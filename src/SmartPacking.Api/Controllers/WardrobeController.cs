using Microsoft.AspNetCore.Mvc;
using SmartPacking.Api.Contracts;
using SmartPacking.Application;
using SmartPacking.Contracts;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/wardrobe")]
public sealed class WardrobeController(ISmartPackingStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResult<IReadOnlyList<ClothingItemResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResult<IReadOnlyList<ClothingItemResponse>>>> GetAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["pagination"] = ["La página debe ser positiva y el tamaño estar entre 1 y 100."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var items = (await store.GetWardrobeAsync(user.Id, cancellationToken)).Where(item => !item.IsDeleted).Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.ToResponse()).ToArray();
        return Ok(new ApiResult<IReadOnlyList<ClothingItemResponse>>(items));
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<ClothingItemResponse>>>> GetDeletedAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["pagination"] = ["La página debe ser positiva y el tamaño estar entre 1 y 100."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var items = (await store.GetWardrobeAsync(user.Id, cancellationToken)).Where(item => item.IsDeleted).Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.ToResponse()).ToArray();
        return Ok(new ApiResult<IReadOnlyList<ClothingItemResponse>>(items));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<ClothingItemResponse>>> CreateAsync(UpsertClothingItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.WarmthLevel is < 1 or > 10)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["clothingItem"] = ["Introduce datos válidos para la prenda."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var created = await store.AddClothingItemAsync(user.Id, request.ToDomain(Guid.NewGuid()), cancellationToken);
        return Created($"/api/wardrobe/{created.Id}", new ApiResult<ClothingItemResponse>(created.ToResponse()));
    }

    [HttpPut("{clothingItemId:guid}")]
    public async Task<ActionResult<ApiResult<ClothingItemResponse>>> UpdateAsync(Guid clothingItemId, UpsertClothingItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.WarmthLevel is < 1 or > 10)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["clothingItem"] = ["Introduce datos válidos para la prenda."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var existing = (await store.GetWardrobeAsync(user.Id, cancellationToken)).SingleOrDefault(item => item.Id == clothingItemId);
        if (existing is null)
        {
            return NotFoundProblem(clothingItemId);
        }

        var updated = await store.UpdateClothingItemAsync(user.Id, request.ToDomain(clothingItemId, existing.IsDeleted), cancellationToken);
        return updated is null ? NotFoundProblem(clothingItemId) : Ok(new ApiResult<ClothingItemResponse>(updated.ToResponse()));
    }

    [HttpPut("{clothingItemId:guid}/status")]
    public async Task<IActionResult> UpdateStatusAsync(Guid clothingItemId, UpdateClothingStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.UpdateClothingStatusAsync(user.Id, clothingItemId, request.IsClean, request.IsAvailable, cancellationToken) ? NoContent() : NotFoundProblem(clothingItemId);
    }

    [HttpDelete("{clothingItemId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.DeleteClothingItemAsync(user.Id, clothingItemId, cancellationToken) ? NoContent() : NotFoundProblem(clothingItemId);
    }

    [HttpPost("{clothingItemId:guid}/restore")]
    public async Task<IActionResult> RestoreAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.RestoreClothingItemAsync(user.Id, clothingItemId, cancellationToken) ? NoContent() : NotFoundProblem(clothingItemId);
    }

    private ObjectResult NotFoundProblem(Guid clothingItemId) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Prenda no encontrada",
        detail: $"No existe una prenda con el identificador '{clothingItemId}'.");
}
