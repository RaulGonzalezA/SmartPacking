using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartPacking.Client;

namespace SmartPacking.Mobile;

public interface IMobileAuthenticationService
{
    Task<bool> HasSessionAsync(CancellationToken cancellationToken);
    Task LoginAsync(CancellationToken cancellationToken);
    Task LogoutAsync();
}

public sealed class SecureAccessTokenProvider(MobileOptions options) : IAccessTokenProvider, IDisposable
{
    private const string AccessTokenKey = "smartpacking.access_token";
    private const string RefreshTokenKey = "smartpacking.refresh_token";
    private const string ExpiresAtKey = "smartpacking.access_token_expires_at";
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    public async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var stored = await ReadAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(stored.AccessToken) &&
            stored.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return stored.AccessToken;
        }

        return await RefreshAccessTokenAsync(stored.AccessToken, cancellationToken);
    }

    public async ValueTask<string?> RefreshAccessTokenAsync(string? rejectedAccessToken, CancellationToken cancellationToken)
    {
        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadAccessTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(current.AccessToken) &&
                !string.Equals(current.AccessToken, rejectedAccessToken, StringComparison.Ordinal) &&
                current.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
            {
                return current.AccessToken;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                Clear();
                return null;
            }

            options.Validate();
            using var httpClient = new HttpClient();
            using var response = await httpClient.PostAsync(
                $"{options.Auth0Authority.TrimEnd('/')}/oauth/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = options.Auth0ClientId,
                    ["refresh_token"] = refreshToken
                }),
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                Clear();
                return null;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var token = await JsonSerializer.DeserializeAsync<OAuthTokenResponse>(stream, cancellationToken: cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresIn <= 0)
            {
                Clear();
                return null;
            }

            await SaveAsync(
                token.AccessToken,
                string.IsNullOrWhiteSpace(token.RefreshToken) ? refreshToken : token.RefreshToken,
                token.ExpiresIn,
                cancellationToken);
            return token.AccessToken;
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public async Task SaveAsync(
        string accessToken,
        string refreshToken,
        int expiresInSeconds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expiresInSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresInSeconds));
        }

        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
        await SecureStorage.Default.SetAsync(
            ExpiresAtKey,
            DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds).ToString("O", CultureInfo.InvariantCulture));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
    }

    public void Dispose() => refreshGate.Dispose();

    private static async Task<StoredAccessToken> ReadAccessTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expiresAtText = await SecureStorage.Default.GetAsync(ExpiresAtKey);
        var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
        cancellationToken.ThrowIfCancellationRequested();

        _ = DateTimeOffset.TryParseExact(
            expiresAtText,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt);
        return new StoredAccessToken(accessToken, expiresAt);
    }

    private sealed record StoredAccessToken(string? AccessToken, DateTimeOffset ExpiresAt);
}

public sealed class MobileAuthenticationService(MobileOptions options, SecureAccessTokenProvider accessTokenProvider) : IMobileAuthenticationService
{
    public async Task<bool> HasSessionAsync(CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(await accessTokenProvider.GetAccessTokenAsync(cancellationToken));

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        options.Validate();
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var callback = new Uri(options.RedirectUri);
        var authorizationUri = new Uri(
            $"{options.Auth0Authority.TrimEnd('/')}/authorize" +
            $"?client_id={Uri.EscapeDataString(options.Auth0ClientId)}" +
            "&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(options.RedirectUri)}" +
            $"&audience={Uri.EscapeDataString(options.Auth0Audience)}" +
            "&scope=openid%20profile%20email%20offline_access" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            "&code_challenge_method=S256" +
            $"&state={Uri.EscapeDataString(state)}");

        var result = await WebAuthenticator.Default.AuthenticateAsync(authorizationUri, callback, cancellationToken);
        if (!result.Properties.TryGetValue("state", out var returnedState) ||
            !string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Auth0 devolvió un estado de autenticación no válido.");
        }

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
        var token = await JsonSerializer.DeserializeAsync<OAuthTokenResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Auth0 devolvió una respuesta de token vacía.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Auth0 no devolvió access_token.");
        }

        if (token.ExpiresIn <= 0)
        {
            throw new InvalidOperationException("Auth0 devolvió expires_in no válido.");
        }

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new InvalidOperationException("Auth0 no devolvió refresh_token. Habilita Offline Access y Refresh Token Rotation para la aplicación Native.");
        }

        await accessTokenProvider.SaveAsync(token.AccessToken, token.RefreshToken, token.ExpiresIn, cancellationToken);
    }

    public Task LogoutAsync()
    {
        accessTokenProvider.Clear();
        return Task.CompletedTask;
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

internal sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);
