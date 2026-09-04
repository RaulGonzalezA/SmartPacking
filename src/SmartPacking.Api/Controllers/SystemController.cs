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

    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile>> UpdateCurrentUserAsync(UpdateCurrentUserRequest request, CancellationToken cancellationToken)
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
        var address = request.Address?.Trim();
        if (address?.Length > 160)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["address"] = ["La dirección o ciudad de salida no puede superar 160 caracteres."]
            }));
        }

        var updated = await store.UpdateUserProfileAsync(user.Id, name, address, cancellationToken);
        return updated is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Usuario no encontrado")
            : Ok(updated);
    }

    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteCurrentUserAsync(DeleteCurrentUserRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Confirmation, "ELIMINAR", StringComparison.Ordinal))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["confirmation"] = ["Escribe ELIMINAR para confirmar el borrado de tus datos locales."]
            }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        await store.DeleteUserDataAsync(user.Id, cancellationToken);
        return NoContent();
    }
}
