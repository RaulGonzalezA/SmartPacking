using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using SmartPacking.Application;

namespace SmartPacking.Api.Identity;

public class HttpContextExternalIdentityAccessor : IExternalIdentityAccessor
{
    private readonly IHttpContextAccessor _http;

    public HttpContextExternalIdentityAccessor(IHttpContextAccessor http) => _http = http;

    public ExternalIdentity? GetCurrent()
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }
        var issuer = user.FindFirst("iss")?.Value ?? string.Empty;
        var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value ?? string.Empty;
        var displayName = user.Identity?.Name ?? user.FindFirst("name")?.Value ?? string.Empty;

        return new ExternalIdentity(issuer, subject, displayName);
    }
}
