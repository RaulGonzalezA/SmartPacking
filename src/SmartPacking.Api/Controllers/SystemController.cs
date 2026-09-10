using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SmartPacking.Application;
using SmartPacking.Api.Validation;
using SmartPacking.Domain;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SystemController(
    ISmartPackingStore store,
    IPhotoStorage photoStorage,
    IValidator<CompleteUserOnboardingRequest> onboardingValidator,
    IValidator<UpdateCurrentUserRequest> updateValidator,
    IValidator<DeleteCurrentUserRequest> deleteValidator) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken) => Ok(await store.GetDefaultUserAsync(cancellationToken));

    [HttpPost("me/onboarding")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserProfile>> CompleteOnboardingAsync(CompleteUserOnboardingRequest request, CancellationToken cancellationToken)
    {
        var validationProblem = await onboardingValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok(await store.CompleteUserOnboardingAsync(user.Id, request.Name.Trim(), cancellationToken));
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile>> UpdateCurrentUserAsync(UpdateCurrentUserRequest request, CancellationToken cancellationToken)
    {
        var validationProblem = await updateValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var address = new UserAddress(request.Street?.Trim(), request.PostalCode?.Trim(), request.City?.Trim(), request.Region?.Trim());
        var updated = await store.UpdateUserProfileAsync(user.Id, request.Name.Trim(), address.DisplayAddress is null ? null : address, cancellationToken);
        return updated is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Usuario no encontrado")
            : Ok(updated);
    }

    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteCurrentUserAsync(DeleteCurrentUserRequest request, CancellationToken cancellationToken)
    {
        var validationProblem = await deleteValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var photoIds = (await store.GetWardrobeAsync(user.Id, cancellationToken))
            .Where(item => !string.IsNullOrWhiteSpace(item.PhotoUrl))
            .Select(item => item.Id)
            .ToArray();
        foreach (var photoId in photoIds)
        {
            await photoStorage.DeleteAsync(photoId, cancellationToken);
        }

        await store.DeleteUserDataAsync(user.Id, cancellationToken);
        return NoContent();
    }
}
