namespace SmartPacking.Application;

public interface IPhotoStorage
{
    Task<string> SaveJpegAsync(Guid clothingItemId, Stream content, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(Guid clothingItemId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid clothingItemId, CancellationToken cancellationToken);
}
