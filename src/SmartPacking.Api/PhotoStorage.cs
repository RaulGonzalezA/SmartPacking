using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SmartPacking.Api;

public interface IPhotoStorage
{
    Task<string> SaveJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken);
}

public sealed class LocalPhotoStorage(IWebHostEnvironment environment) : IPhotoStorage
{
    public async Task<string> SaveJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(environment.WebRootPath, "uploads");
        Directory.CreateDirectory(directory);
        await using var output = File.Create(Path.Combine(directory, $"{clothingItemId}.jpg"));
        await content.CopyToAsync(output, cancellationToken);
        return $"/uploads/{clothingItemId}.jpg";
    }
}

public sealed class BlobPhotoStorage(BlobServiceClient blobServiceClient, IConfiguration configuration) : IPhotoStorage
{
    private readonly string containerName = configuration["Storage:Container"] ?? "wardrobe";
    private readonly string publicBaseUrl = configuration["Storage:PublicBaseUrl"] ?? throw new InvalidOperationException("Storage:PublicBaseUrl es obligatoria para Blob Storage.");

    public async Task<string> SaveJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        var blob = container.GetBlobClient($"{clothingItemId}.jpg");
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" } }, cancellationToken);
        return $"{publicBaseUrl.TrimEnd('/')}/{containerName}/{clothingItemId}.jpg";
    }
}
