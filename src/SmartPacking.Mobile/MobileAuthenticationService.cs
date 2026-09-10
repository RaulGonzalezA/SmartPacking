using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPacking.Client;

namespace SmartPacking.Mobile;

public interface IMobileAuthenticationService
{
    Task<bool> HasSessionAsync(CancellationToken cancellationToken);
    Task LoginAsync(CancellationToken cancellationToken);
    Task LogoutAsync();
}

public sealed class SecureAccessTokenProvider : IAccessTokenProvider
{
    private const string AccessTokenKey = "smartpacking.access_token";
    private const string ExpiresAtKey = "smartpacking.access_token_expires_at";

    public async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expiresAtText = await SecureStorage.Default.GetAsync(ExpiresAtKey);
        if (!DateTimeOffset.TryParse(expiresAtText, out var expiresAt) || expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return null;
        }

        return await SecureStorage.Default.GetAsync(AccessTokenKey);
    }

    public static async Task SaveAsync(string accessToken, int expiresInSeconds)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds).ToString("O"));
    }

    public static void Clear()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
    }
}

public sealed class MobileAuthenticationService(MobileOptions options, IAccessTokenProvider accessTokenProvider) : IMobileAuthenticationService
{
    public async Task<bool> HasSessionAsync(CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(await accessTokenProvider.GetAccessTokenAsync(cancellationToken));

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var callback = new Uri(options.RedirectUri);
        var authorizationUri = new Uri(
            $"{options.Auth0Authority.TrimEnd('/')}/authorize" +
            $"?client_id={Uri.EscapeDataString(options.Auth0ClientId)}" +
            "&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(options.RedirectUri)}" +
            $"&audience={Uri.EscapeDataString(options.Auth0Audience)}" +
            "&scope=openid%20profile%20email" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            "&code_challenge_method=S256");

        var result = await WebAuthenticator.Default.AuthenticateAsync(authorizationUri, callback);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Auth0 no devolvió el código de autorización.");
        }

        using var httpClient = new HttpClient();
        using var tokenResponse = await httpClient.PostAsync(
            $"{options.Auth0Authority.TrimEnd('/')}/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = options.Auth0ClientId,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = options.RedirectUri
            }),
            cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        await using var stream = await tokenResponse.Content.ReadAsStreamAsync(cancellationToken);
        var token = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Auth0 devolvió una respuesta de token vacía.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Auth0 no devolvió access_token.");
        }

        await SecureAccessTokenProvider.SaveAsync(token.AccessToken, Math.Max(60, token.ExpiresIn));
    }

    public Task LogoutAsync()
    {
        SecureAccessTokenProvider.Clear();
        return Task.CompletedTask;
    }

    private void EnsureConfigured()
    {
        if (options.Auth0Authority.Contains("YOUR_AUTH0_DOMAIN", StringComparison.Ordinal) ||
            options.Auth0ClientId.Contains("YOUR_NATIVE_AUTH0_CLIENT_ID", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Configura Auth0Authority y Auth0ClientId de la aplicación Native de Auth0 en MobileOptions.");
        }
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}
