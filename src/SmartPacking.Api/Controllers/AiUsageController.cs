using Microsoft.AspNetCore.Mvc;
using SmartPacking.Application;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/me/ai-usage")]
public sealed class AiUsageController(IGarmentRecognitionUsageService recognitionUsage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(GarmentRecognitionUsageResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GarmentRecognitionUsageResult>> GetAsync(CancellationToken cancellationToken) => Ok(await recognitionUsage.GetUsageAsync(cancellationToken));
}
