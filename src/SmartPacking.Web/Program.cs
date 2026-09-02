using Microsoft.AspNetCore.DataProtection;
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
    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(clientId))
    {
        throw new InvalidOperationException("Configura Authentication:OpenIdConnect:Authority y ClientId para activar OAuth 2.0.");
    }

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
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
    });
    builder.Services.AddAuthorization();
}
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys")));
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("La configuración Api:BaseUrl es obligatoria.");
builder.Services.AddTransient<ApiProblemDetailsHandler>();
builder.Services.AddHttpClient<SmartPackingApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl)).AddHttpMessageHandler<ApiProblemDetailsHandler>();
builder.Services.AddScoped<IWebSmartPackingClient>(provider => provider.GetRequiredService<SmartPackingApiClient>());

var app = builder.Build();
app.UseStaticFiles();
if (authenticationEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapGet("/login", () => Results.Redirect("/login.html"));
    app.MapGet("/auth/login", () => Results.Challenge(new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" }, [OpenIdConnectDefaults.AuthenticationScheme]));
    app.MapGet("/auth/register", () => Results.Challenge(new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" }, [OpenIdConnectDefaults.AuthenticationScheme]));
    app.MapGet("/auth/logout", () => Results.SignOut(new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" }, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
}
app.UseAntiforgery();
var components = app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
if (authenticationEnabled)
{
    components.RequireAuthorization();
}
await app.RunAsync();
