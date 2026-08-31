using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmartPacking.Api;

public sealed class StorageHealthCheck(BlobServiceClient? blobServiceClient = null) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (blobServiceClient is null)
        {
            return HealthCheckResult.Healthy("Se está usando almacenamiento local.");
        }

        try
        {
            await blobServiceClient.GetAccountInfoAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("Blob Storage está disponible.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Blob Storage no está disponible.", exception);
        }
    }
}
