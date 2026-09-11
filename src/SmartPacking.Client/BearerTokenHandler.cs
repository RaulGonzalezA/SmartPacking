using System.Net;
using System.Net.Http.Headers;

namespace SmartPacking.Client;

public sealed class BearerTokenHandler(IAccessTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        SetAuthorization(request, accessToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || string.IsNullOrWhiteSpace(accessToken))
        {
            return response;
        }

        var refreshedAccessToken = await tokenProvider.RefreshAccessTokenAsync(accessToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(refreshedAccessToken) ||
            string.Equals(refreshedAccessToken, accessToken, StringComparison.Ordinal))
        {
            return response;
        }

        using var retryRequest = await CloneAsync(request, cancellationToken);
        SetAuthorization(retryRequest, refreshedAccessToken);
        response.Dispose();
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static void SetAuthorization(HttpRequestMessage request, string? accessToken) =>
        request.Headers.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            _ = clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                _ = content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }
}
