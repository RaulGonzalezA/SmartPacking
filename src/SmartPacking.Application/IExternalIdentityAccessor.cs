namespace SmartPacking.Application;

public sealed record ExternalIdentity(string Issuer, string Subject, string DisplayName);

public interface IExternalIdentityAccessor
{
    ExternalIdentity? GetCurrent();
}
