namespace SmartPacking.Client;

public interface IAuthenticationRequiredHandler
{
    Task HandleAsync(CancellationToken cancellationToken);
}
