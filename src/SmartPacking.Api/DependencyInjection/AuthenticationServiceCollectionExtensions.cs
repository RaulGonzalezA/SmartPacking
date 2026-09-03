using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        services.AddAuthorization();
        return true;
    }
}
