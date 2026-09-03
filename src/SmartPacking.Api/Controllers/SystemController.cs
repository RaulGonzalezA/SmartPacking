using Microsoft.AspNetCore.Mvc;
using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SystemController(ISmartPackingStore store) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken) => Ok(await store.GetDefaultUserAsync(cancellationToken));

    [HttpPost("me/onboarding")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserProfile>> CompleteOnboardingAsync(CompleteUserOnboardingRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["name"] = ["Escribe un nombre de entre 1 y 80 caracteres."]
            }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok(await store.CompleteUserOnboardingAsync(user.Id, name, cancellationToken));
    }
}
