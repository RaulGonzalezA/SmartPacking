using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Api.Authentication;
using SmartPacking.Application;

namespace SmartPacking.Api.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    public static bool AddSmartPackingAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("Authentication:Enabled");
        services.AddHttpContextAccessor();
        services.AddScoped<IExternalIdentityAccessor, CurrentUserIdentityAccessor>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminUsers", policy => policy.RequireClaim("permissions", "admin:users"));
            options.AddPolicy("AdminPlans", policy => policy.RequireClaim("permissions", "admin:plans"));
            options.AddPolicy("AdminCredits", policy => policy.RequireClaim("permissions", "admin:credits"));
            options.AddPolicy("AdminBilling", policy => policy.RequireClaim("permissions", "admin:billing"));
            options.AddPolicy("AdminAudit", policy => policy.RequireClaim("permissions", "admin:audit"));
        });
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
                        var permissions = context.Principal?.FindAll("permissions")
                            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                            .Distinct(StringComparer.Ordinal)
                            .ToArray() ?? [];
                        foreach (var permission in permissions.Where(permission => !context.Principal!.HasClaim("permissions", permission)))
                        {
                            identity?.AddClaim(new System.Security.Claims.Claim("permissions", permission));
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
