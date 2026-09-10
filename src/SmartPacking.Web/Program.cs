using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using SmartPacking.Application;
using SmartPacking.Web;
using SmartPacking.Web.Components;

var builder = WebApplication.CreateBuilder(args);
var authenticationEnabled = builder.Configuration.GetValue<bool>("Authentication:Enabled");
if (authenticationEnabled)
{
    var authority = builder.Configuration["Authentication:OpenIdConnect:Authority"];
    var clientId = builder.Configuration["Authentication:OpenIdConnect:ClientId"];
    var audience = builder.Configuration["Authentication:OpenIdConnect:Audience"];
    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException("Configura Authentication:OpenIdConnect:Authority, ClientId y Audience para activar Auth0.");
    }

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options => options.LoginPath = "/login")
    .AddOpenIdConnect(options =>
    {
        options.Authority = authority;
        options.ClientId = clientId;
        options.ClientSecret = builder.Configuration["Authentication:OpenIdConnect:ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("admin:users");
        options.Scope.Add("admin:plans");
        options.Scope.Add("admin:credits");
        options.Scope.Add("admin:billing");
        options.Scope.Add("admin:audit");
        options.ClaimActions.MapUniqueJsonKey("email_verified", "email_verified");
        options.ClaimActions.MapJsonKey("permissions", "permissions");
        options.ClaimActions.MapJsonKey("permissions", "https://smartpacking.app/permissions");
        options.ClaimActions.MapJsonKey(System.Security.Claims.ClaimTypes.Role, "roles");
        options.ClaimActions.MapJsonKey(System.Security.Claims.ClaimTypes.Role, "https://smartpacking.app/roles");
        options.Events.OnTokenValidated = context =>
        {
            if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
            {
                Auth0AccessTokenPermissions.AddTo(identity, context.TokenEndpointResponse?.AccessToken);
            }

            return Task.CompletedTask;
        };
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.SetParameter("audience", audience);
            context.ProtocolMessage.SetParameter("ui_locales", "es");
            if (context.Properties.Items.TryGetValue("screen_hint", out var screenHint))
            {
                context.ProtocolMessage.SetParameter("screen_hint", screenHint);
            }

            return Task.CompletedTask;
        };
    });
    builder.Services.AddAuthorization();
}
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys")));
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("La configuración Api:BaseUrl es obligatoria.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ApiProblemDetailsHandler>();
builder.Services.AddTransient<ApiAccessTokenHandler>();
builder.Services.AddHttpClient<SmartPackingApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<ApiAccessTokenHandler>()
    .AddHttpMessageHandler<ApiProblemDetailsHandler>();
builder.Services.AddScoped<IWebSmartPackingClient>(provider => provider.GetRequiredService<SmartPackingApiClient>());
builder.Services.AddScoped<SmartPacking.Web.Components.Pages.HomeDataCoordinator>();

var app = builder.Build();
app.MapStaticAssets();
if (authenticationEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapGet("/login", async (HttpContext context, string? error) =>
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.File(Path.Combine(app.Environment.WebRootPath, "login.html"), "text/html");
    });
    app.MapGet("/auth/login", () => Results.Challenge(new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" }, [OpenIdConnectDefaults.AuthenticationScheme]));
    app.MapGet("/auth/register", () =>
    {
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" };
        properties.Items["screen_hint"] = "signup";
        return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
    });
    app.MapGet("/auth/logout", () => Results.SignOut(new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/login" }, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
}
app.UseAntiforgery();
var components = app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
if (authenticationEnabled)
{
    components.RequireAuthorization();
}
await app.RunAsync();
