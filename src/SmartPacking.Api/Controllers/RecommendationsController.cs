using Microsoft.AspNetCore.Mvc;
using SmartPacking.Application;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
public sealed class RecommendationsController(ISmartPackingStore store, PackingListService packingLists) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var plan = await packingLists.GetOrCreateAsync(user.Id, DemoData.RomeTrip.Id, cancellationToken);
        return plan is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Viaje no encontrado")
            : Ok(plan);
    }
}
