namespace SmartPacking.Application;

public sealed record ExternalIdentity(string Issuer, string Subject, string DisplayName, bool IsEmailVerified = false);

public interface IExternalIdentityAccessor
{
    ExternalIdentity? GetCurrent();
}
