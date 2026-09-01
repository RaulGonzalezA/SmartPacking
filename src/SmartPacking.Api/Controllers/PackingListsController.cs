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
        await store.SetPackedAsync(user.Id, packingListId, clothingItemId, request.IsPacked, cancellationToken);
        return NoContent();
    }

    [HttpPut("profile-packing-lists/{packingListId:guid}/items/{clothingItemId:guid}")]
    public async Task<IActionResult> SetProfilePackedAsync(Guid packingListId, Guid clothingItemId, SetPackedRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        await store.SetProfilePackedAsync(user.Id, packingListId, clothingItemId, request.IsPacked, cancellationToken);
        return NoContent();
    }

    [HttpPost("profile-packing-lists/{packingListId:guid}/items")]
    public async Task<IActionResult> AddProfilePackedItemAsync(Guid packingListId, AddPackingListItemRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.AddProfilePackingListItemAsync(user.Id, packingListId, request.ClothingItemId, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Prenda o maleta no encontrada");
    }

    [HttpPut("checklist/{itemId:guid}")]
    public async Task<IActionResult> SetChecklistPackedAsync(Guid itemId, SetPackedRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        await store.SetChecklistPackedAsync(user.Id, itemId, request.IsPacked, cancellationToken);
        return NoContent();
    }
}
