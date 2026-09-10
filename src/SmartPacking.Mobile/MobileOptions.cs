namespace SmartPacking.Mobile;

public sealed record MobileOptions(
    string ApiBaseAddress,
    string Auth0Authority,
    string Auth0ClientId,
    string Auth0Audience,
    string RedirectUri)
{
#pragma warning disable S5332 // Android emulator reaches the local development API over HTTP only.
    public static MobileOptions Development { get; } = new(
        "http://10.0.2.2:8080/",
        "https://YOUR_AUTH0_DOMAIN/",
        "YOUR_NATIVE_AUTH0_CLIENT_ID",
        "https://smartpacking-api",
        "smartpacking://callback");
#pragma warning restore S5332

    public static MobileOptions Production { get; } = new(
        "https://YOUR_API_HOST/",
        "https://YOUR_AUTH0_DOMAIN/",
        "YOUR_NATIVE_AUTH0_CLIENT_ID",
        "https://smartpacking-api",
        "smartpacking://callback");

    public static MobileOptions Current
    {
        get
        {
#if DEBUG
            return Development;
#else
            return Production;
#endif
        }
    }

    public void Validate()
    {
        if (ApiBaseAddress.Contains("YOUR_API_HOST", StringComparison.Ordinal) ||
            Auth0Authority.Contains("YOUR_AUTH0_DOMAIN", StringComparison.Ordinal) ||
            Auth0ClientId.Contains("YOUR_NATIVE_AUTH0_CLIENT_ID", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Configura la API y Auth0 de la aplicación Native en MobileOptions antes de ejecutar esta compilación.");
        }

        if (!Uri.TryCreate(ApiBaseAddress, UriKind.Absolute, out var apiUri) ||
            !Uri.TryCreate(Auth0Authority, UriKind.Absolute, out var authorityUri) ||
            !Uri.TryCreate(RedirectUri, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("La configuración móvil contiene una URL no válida.");
        }

#if !DEBUG
        if (!string.Equals(apiUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Las compilaciones Release requieren HTTPS para la API y Auth0.");
        }
#endif
    }
}
