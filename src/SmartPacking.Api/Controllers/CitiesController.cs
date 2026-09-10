using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPacking.Application;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/cities")]
[AllowAnonymous]
public sealed class CitiesController(IWeatherProvider weather) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync([FromQuery] string? query, CancellationToken cancellationToken) => Ok(await weather.SearchCitiesAsync(query ?? string.Empty, cancellationToken));
}
