using Microsoft.AspNetCore.Mvc;
using SmartPacking.Api;
using SmartPacking.Application;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class PackingListsController(ISmartPackingStore store) : ControllerBase
{
    [HttpPut("packing-lists/{packingListId:guid}/items/{clothingItemId:guid}")]
    public async Task<IActionResult> SetPackedAsync(Guid packingListId, Guid clothingItemId, SetPackedRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.SetPackedAsync(user.Id, packingListId, clothingItemId, request.IsPacked, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Prenda o maleta no encontrada");
    }

    [HttpPut("profile-packing-lists/{packingListId:guid}/items/{clothingItemId:guid}")]
    public async Task<IActionResult> SetProfilePackedAsync(Guid packingListId, Guid clothingItemId, SetPackedRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.SetProfilePackedAsync(user.Id, packingListId, clothingItemId, request.IsPacked, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Prenda o maleta no encontrada");
    }

    [HttpPost("profile-packing-lists/{packingListId:guid}/items")]
    public async Task<IActionResult> AddProfilePackedItemAsync(Guid packingListId, AddPackingListItemRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.AddProfilePackingListItemAsync(user.Id, packingListId, request.ClothingItemId, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Prenda o maleta no encontrada");
    }

    [HttpPost("profile-packing-lists/{packingListId:guid}/recommendation-changes/apply")]
    public async Task<IActionResult> ApplyRecommendationChangeAsync(Guid packingListId, ResolveRecommendationChangeRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.ApplyProfileRecommendationChangeAsync(user.Id, packingListId, request.ClothingItemId, request.ChangeKind, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Cambio o maleta no encontrado");
    }

    [HttpPost("profile-packing-lists/{packingListId:guid}/recommendation-changes/ignore")]
    public async Task<IActionResult> IgnoreRecommendationChangeAsync(Guid packingListId, ResolveRecommendationChangeRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.IgnoreProfileRecommendationChangeAsync(user.Id, packingListId, request.ClothingItemId, request.ChangeKind, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Cambio o maleta no encontrado");
    }

    [HttpPut("checklist/{itemId:guid}")]
    public async Task<IActionResult> SetChecklistPackedAsync(Guid itemId, SetPackedRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.SetChecklistPackedAsync(user.Id, itemId, request.IsPacked, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Checklist no encontrada");
    }
}
