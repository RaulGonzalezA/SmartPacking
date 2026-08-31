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
        if (!(await store.GetWardrobeAsync(user.Id, cancellationToken)).Any(item => item.Id == clothingItemId))
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Prenda no encontrada");
        }

        await using var photoStream = photo.OpenReadStream();
        var imageUrl = await photoStorage.SaveJpegAsync(clothingItemId, photoStream, cancellationToken);
        return Ok(new ApiResult<PhotoUploadResponse>(new PhotoUploadResponse(imageUrl)));
    }

    public sealed record PhotoUploadResponse(string ImageUrl);
}
