namespace SmartPacking.Mobile;

public sealed record MobileOptions(
    string ApiBaseAddress,
    string Auth0Authority,
    string Auth0ClientId,
    string Auth0Audience,
    string RedirectUri)
{
    public static MobileOptions Development { get; } = new(
        "http://10.0.2.2:8080/",
        "https://YOUR_AUTH0_DOMAIN/",
        "YOUR_NATIVE_AUTH0_CLIENT_ID",
        "https://smartpacking-api",
        "smartpacking://callback");
}
