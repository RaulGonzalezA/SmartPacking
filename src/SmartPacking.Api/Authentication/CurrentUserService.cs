using System.Security.Claims;
using SmartPacking.Application;

namespace SmartPacking.Api.Authentication;

public sealed class CurrentUserIdentityAccessor(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : IExternalIdentityAccessor
{
    public ExternalIdentity? GetCurrent()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("El token no contiene el claim sub.");
        var issuer = principal.FindFirstValue("iss") ?? configuration["Authentication:JwtBearer:Authority"]
            ?? throw new InvalidOperationException("No se ha podido determinar el issuer del token.");
        var displayName = principal.FindFirstValue("name") ?? principal.FindFirstValue("preferred_username") ?? principal.FindFirstValue(ClaimTypes.Email) ?? subject;
        return new ExternalIdentity(issuer, subject, displayName);
    }
}
