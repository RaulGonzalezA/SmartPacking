using Microsoft.AspNetCore.Authentication;

namespace SmartPacking.Web;

public sealed class ApiAccessTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var accessToken = httpContext?.User.Identity?.IsAuthenticated == true
            ? await httpContext.GetTokenAsync("access_token")
            : null;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
