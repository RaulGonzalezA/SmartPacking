using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure;
using SmartPacking.Application;

namespace SmartPacking.Api;

public sealed class LocalPhotoStorage(IWebHostEnvironment environment) : IPhotoStorage
{
    public async Task<string> SaveJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(environment.WebRootPath, "uploads");
        Directory.CreateDirectory(directory);
        await using var output = File.Create(Path.Combine(directory, $"{clothingItemId}.jpg"));
        await content.CopyToAsync(output, cancellationToken);
        return $"/api/wardrobe/{clothingItemId}/photo";
    }

    public Task<Stream?> OpenReadAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.WebRootPath, "uploads", $"{clothingItemId}.jpg");
        Stream? result = File.Exists(path) ? File.OpenRead(path) : null;
        return Task.FromResult(result);
    }

    public Task DeleteAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.WebRootPath, "uploads", $"{clothingItemId}.jpg");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}

public sealed class BlobPhotoStorage(BlobServiceClient blobServiceClient, IConfiguration configuration) : IPhotoStorage
{
    private readonly string containerName = configuration["Storage:Container"] ?? "wardrobe";

    public async Task<string> SaveJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await container.SetAccessPolicyAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var blob = container.GetBlobClient($"{clothingItemId}.jpg");
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" } }, cancellationToken);
        return $"/api/wardrobe/{clothingItemId}/photo";
    }

    public async Task<Stream?> OpenReadAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await blobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient($"{clothingItemId}.jpg")
                .DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        await blobServiceClient
            .GetBlobContainerClient(containerName)
            .GetBlobClient($"{clothingItemId}.jpg")
            .DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
