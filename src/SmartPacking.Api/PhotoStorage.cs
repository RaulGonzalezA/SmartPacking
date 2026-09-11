using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SmartPacking.Application;

namespace SmartPacking.Api;

public sealed class LocalPhotoStorage(IWebHostEnvironment environment) : IPhotoStorage
{
    public async Task<string> SaveJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken)
    {
        await SaveAsync(GetPhotoPath(clothingItemId), content, cancellationToken);
        return $"/api/wardrobe/{clothingItemId}/photo";
    }

    public Task SaveThumbnailJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken) =>
        SaveAsync(GetThumbnailPath(clothingItemId), content, cancellationToken);

    public Task<Stream?> OpenReadAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        OpenAsync(GetPhotoPath(clothingItemId), cancellationToken);

    public Task<Stream?> OpenThumbnailReadAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        OpenAsync(GetThumbnailPath(clothingItemId), cancellationToken);

    public Task DeleteThumbnailAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteIfExists(GetThumbnailPath(clothingItemId));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteIfExists(GetPhotoPath(clothingItemId));
        DeleteIfExists(GetThumbnailPath(clothingItemId));
        return Task.CompletedTask;
    }

    private string GetPhotoPath(Guid clothingItemId) =>
        Path.Combine(environment.WebRootPath, "uploads", $"{clothingItemId}.jpg");

    private string GetThumbnailPath(Guid clothingItemId) =>
        Path.Combine(environment.WebRootPath, "uploads", $"{clothingItemId}.thumb.jpg");

    private static async Task SaveAsync(string path, Stream content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("No se pudo resolver el directorio de fotografías.");
        Directory.CreateDirectory(directory);
        await using var output = File.Create(path);
        await content.CopyToAsync(output, cancellationToken);
    }

    private static Task<Stream?> OpenAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream? result = File.Exists(path) ? File.OpenRead(path) : null;
        return Task.FromResult(result);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

public sealed class BlobPhotoStorage(BlobServiceClient blobServiceClient, IConfiguration configuration) : IPhotoStorage
{
    private readonly string containerName = configuration["Storage:Container"] ?? "wardrobe";

    public async Task<string> SaveJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken)
    {
        await UploadAsync(GetPhotoBlobName(clothingItemId), content, cancellationToken);
        return $"/api/wardrobe/{clothingItemId}/photo";
    }

    public Task SaveThumbnailJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken) =>
        UploadAsync(GetThumbnailBlobName(clothingItemId), content, cancellationToken);

    public Task<Stream?> OpenReadAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        OpenAsync(GetPhotoBlobName(clothingItemId), cancellationToken);

    public Task<Stream?> OpenThumbnailReadAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        OpenAsync(GetThumbnailBlobName(clothingItemId), cancellationToken);

    public async Task DeleteThumbnailAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        await GetContainer()
            .GetBlobClient(GetThumbnailBlobName(clothingItemId))
            .DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        var container = GetContainer();
        await container.GetBlobClient(GetPhotoBlobName(clothingItemId)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        await container.GetBlobClient(GetThumbnailBlobName(clothingItemId)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private BlobContainerClient GetContainer() => blobServiceClient.GetBlobContainerClient(containerName);

    private static string GetPhotoBlobName(Guid clothingItemId) => $"{clothingItemId}.jpg";

    private static string GetThumbnailBlobName(Guid clothingItemId) => $"{clothingItemId}.thumb.jpg";

    private async Task UploadAsync(string blobName, Stream content, CancellationToken cancellationToken)
    {
        var container = GetContainer();
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await container.SetAccessPolicyAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await container.GetBlobClient(blobName).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" } },
            cancellationToken);
    }

    private async Task<Stream?> OpenAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await GetContainer()
                .GetBlobClient(blobName)
                .DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }
}
