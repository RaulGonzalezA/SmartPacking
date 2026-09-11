using Microsoft.AspNetCore.Mvc;
using SmartPacking.Api.Contracts;
using SmartPacking.Application;
using SmartPacking.Contracts;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/wardrobe")]
public sealed class WardrobePhotosController(ISmartPackingStore store, IPhotoStorage photoStorage) : ControllerBase
{
    [HttpGet("{clothingItemId:guid}/photo")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        GetPhotoAsync(clothingItemId, thumbnail: false, cancellationToken);

    [HttpGet("{clothingItemId:guid}/thumbnail")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetThumbnailAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        GetPhotoAsync(clothingItemId, thumbnail: true, cancellationToken);

    [HttpPost("{clothingItemId:guid}/photo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResult<PhotoUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadAsync(
        Guid clothingItemId,
        [FromForm] IFormFile photo,
        [FromForm] IFormFile? thumbnail,
        CancellationToken cancellationToken)
    {
        if (!IsValidJpeg(photo, 5 * 1024 * 1024))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["photo"] = ["Selecciona una foto JPEG de hasta 5 MB."] }));
        }

        if (thumbnail is not null && !IsValidJpeg(thumbnail, 1024 * 1024))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["thumbnail"] = ["La miniatura debe ser JPEG y ocupar como máximo 1 MB."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var clothingItem = (await store.GetWardrobeAsync(user.Id, cancellationToken)).SingleOrDefault(item => item.Id == clothingItemId);
        if (clothingItem is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Prenda no encontrada");
        }

        await using var photoStream = photo.OpenReadStream();
        var imageUrl = await photoStorage.SaveJpegAsync(clothingItemId, photoStream, cancellationToken);

        if (thumbnail is null)
        {
            await photoStorage.DeleteThumbnailAsync(clothingItemId, cancellationToken);
        }
        else
        {
            await using var thumbnailStream = thumbnail.OpenReadStream();
            await photoStorage.SaveThumbnailJpegAsync(clothingItemId, thumbnailStream, cancellationToken);
        }

        await store.UpdateClothingItemAsync(user.Id, clothingItem with { PhotoUrl = imageUrl }, cancellationToken);
        return Ok(new ApiResult<PhotoUploadResponse>(new PhotoUploadResponse(imageUrl)));
    }

    private async Task<IActionResult> GetPhotoAsync(Guid clothingItemId, bool thumbnail, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var clothingItem = (await store.GetWardrobeAsync(user.Id, cancellationToken)).SingleOrDefault(item => item.Id == clothingItemId);
        if (clothingItem is null || string.IsNullOrWhiteSpace(clothingItem.PhotoUrl))
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Foto no encontrada");
        }

        Stream? photo = null;
        if (thumbnail)
        {
            photo = await photoStorage.OpenThumbnailReadAsync(clothingItemId, cancellationToken);
        }

        photo ??= await photoStorage.OpenReadAsync(clothingItemId, cancellationToken);
        if (photo is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Foto no encontrada");
        }

        Response.Headers.CacheControl = "private, max-age=86400";
        return File(photo, "image/jpeg");
    }

    private static bool IsValidJpeg(IFormFile file, long maximumBytes) =>
        file.Length > 0 &&
        file.Length <= maximumBytes &&
        string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase);

    public sealed record PhotoUploadResponse(string ImageUrl);
}
