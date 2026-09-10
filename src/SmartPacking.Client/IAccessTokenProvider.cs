namespace SmartPacking.Client;

public interface IAccessTokenProvider
{
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}
