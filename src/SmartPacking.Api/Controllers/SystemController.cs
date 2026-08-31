using Microsoft.AspNetCore.Mvc;
using SmartPacking.Application;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SystemController(ISmartPackingStore store) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken) => Ok(await store.GetDefaultUserAsync(cancellationToken));
}
