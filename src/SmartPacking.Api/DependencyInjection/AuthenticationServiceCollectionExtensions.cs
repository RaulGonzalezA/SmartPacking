using Microsoft.AspNetCore.Authentication.JwtBearer;
using SmartPacking.Api.Authentication;
using SmartPacking.Application;

namespace SmartPacking.Api.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    private static readonly string[] AdminPermissions = ["admin:users", "admin:plans", "admin:credits", "admin:billing", "admin:audit"];
    private static readonly char[] RoleSeparators = [' ', ',', '"'];
    private const string Admin = "Admin";
    private const string Permissions = "permissions";
    public static bool AddSmartPackingAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("Authentication:Enabled");
        services.AddHttpContextAccessor();
        services.AddScoped<IExternalIdentityAccessor, CurrentUserIdentityAccessor>();
        services.AddAuthorizationBuilder()
            .AddPolicy("AdminUsers", policy => policy.RequireRole(Admin).RequireClaim(Permissions, "admin:users"))
            .AddPolicy("AdminPlans", policy => policy.RequireRole(Admin).RequireClaim(Permissions, "admin:plans"))
            .AddPolicy("AdminCredits", policy => policy.RequireRole(Admin).RequireClaim(Permissions, "admin:credits"))
            .AddPolicy("AdminBilling", policy => policy.RequireRole(Admin).RequireClaim(Permissions, "admin:billing"))
            .AddPolicy("AdminAudit", policy => policy.RequireRole(Admin).RequireClaim(Permissions, "admin:audit"));
        if (!enabled)
        {
            return false;
        }

        var authority = configuration["Authentication:JwtBearer:Authority"];
        var audience = configuration["Authentication:JwtBearer:Audience"];
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Configura Authentication:JwtBearer:Authority y Audience para proteger la API.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var identity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                        var roles = (context.Principal?.Claims ?? [])
                            .Where(claim => claim.Type is "roles" or "https://smartpacking.app/roles")
                            .SelectMany(claim => claim.Value.Trim('[', ']', '"').Split(RoleSeparators, StringSplitOptions.RemoveEmptyEntries))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        foreach (var role in roles.Where(role => !context.Principal!.IsInRole(role)))
                        {
                            identity?.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
                        }

                        var permissions = (context.Principal?.Claims ?? [])
                            .Where(claim => claim.Type is Permissions or "permission")
                            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                            .SelectMany(permission => permission == "admin:*" ? AdminPermissions : [permission])
                            .Distinct(StringComparer.Ordinal)
                            .ToArray() ?? [];
                        foreach (var permission in permissions.Where(permission => !context.Principal!.HasClaim(Permissions, permission)))
                        {
                            identity?.AddClaim(new System.Security.Claims.Claim(Permissions, permission));
                        }

                        var emailVerified = context.Principal?.FindFirst("https://smartpacking.app/email_verified")?.Value
                            ?? context.Principal?.FindFirst("email_verified")?.Value;
                        if (!bool.TryParse(emailVerified, out var isEmailVerified) || !isEmailVerified)
                        {
                            context.Fail("El correo electrónico debe estar verificado para usar SmartPacking.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });
        return true;
    }
}
