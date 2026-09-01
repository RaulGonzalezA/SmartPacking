using Microsoft.AspNetCore.Mvc;
using SmartPacking.Api.Contracts;
using SmartPacking.Application;
using SmartPacking.Contracts;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/wardrobe")]
public sealed class WardrobePhotosController(ISmartPackingStore store, IPhotoStorage photoStorage) : ControllerBase
{
    [HttpPost("{clothingItemId:guid}/photo")]
    public async Task<IActionResult> UploadAsync(Guid clothingItemId, IFormFile photo, CancellationToken cancellationToken)
    {
        if (photo.Length == 0 || photo.Length > 5 * 1024 * 1024 || !string.Equals(photo.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["photo"] = ["Selecciona una foto JPEG de hasta 5 MB."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var clothingItem = (await store.GetWardrobeAsync(user.Id, cancellationToken)).SingleOrDefault(item => item.Id == clothingItemId);
        if (clothingItem is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Prenda no encontrada");
        }

        await using var photoStream = photo.OpenReadStream();
        var imageUrl = await photoStorage.SaveJpegAsync(clothingItemId, photoStream, cancellationToken);
        await store.UpdateClothingItemAsync(user.Id, clothingItem with { PhotoUrl = imageUrl }, cancellationToken);
        return Ok(new ApiResult<PhotoUploadResponse>(new PhotoUploadResponse(imageUrl)));
    }

    public sealed record PhotoUploadResponse(string ImageUrl);
}
