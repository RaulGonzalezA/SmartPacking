using Microsoft.AspNetCore.Mvc;
using SmartPacking.Api;
using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ProfilesController(ISmartPackingStore store, ProfilePackingListService profilePackingLists) : ControllerBase
{
    [HttpGet("profiles")]
    public async Task<ActionResult<IReadOnlyList<FamilyProfile>>> GetAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok(await store.GetFamilyProfilesAsync(user.Id, cancellationToken));
    }

    [HttpPost("profiles")]
    public async Task<ActionResult<FamilyProfile>> CreateAsync(CreateFamilyProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = ["Escribe un nombre para el perfil."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var profile = await store.AddFamilyProfileAsync(user.Id, new FamilyProfile(Guid.NewGuid(), request.Name.Trim(), false, request.PackingNotes?.Trim(), request.MedicalNotes?.Trim()), cancellationToken);
        return Created($"/api/profiles/{profile.Id}", profile);
    }

    [HttpPut("profiles/{profileId:guid}")]
    public async Task<ActionResult<FamilyProfile>> UpdateAsync(Guid profileId, UpdateFamilyProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = ["Escribe un nombre para el perfil."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var profile = await store.UpdateFamilyProfileAsync(user.Id, new FamilyProfile(profileId, request.Name.Trim(), false, request.PackingNotes?.Trim(), request.MedicalNotes?.Trim()), cancellationToken);
        return profile is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Viajero no encontrado") : Ok(profile);
    }

    [HttpDelete("profiles/{profileId:guid}")]
    public async Task<IActionResult> ArchiveAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.ArchiveFamilyProfileAsync(user.Id, profileId, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "El viajero no existe o es el perfil principal");
    }

    [HttpGet("trips/{tripId:guid}/profiles")]
    public async Task<ActionResult<IReadOnlyList<FamilyProfile>>> GetTripProfilesAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var profiles = await store.GetTripProfilesAsync(user.Id, tripId, cancellationToken);
        if (profiles.Count == 0 && await store.GetTripAsync(user.Id, tripId, cancellationToken) is not null)
        {
            await store.SetTripProfilesAsync(user.Id, tripId, [user.Id], cancellationToken);
            profiles = await store.GetTripProfilesAsync(user.Id, tripId, cancellationToken);
        }

        return Ok(profiles);
    }

    [HttpPut("trips/{tripId:guid}/profiles")]
    public async Task<IActionResult> SetTripProfilesAsync(Guid tripId, SetTripProfilesRequest request, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Viaje no encontrado");
        }

        await store.SetTripProfilesAsync(user.Id, tripId, request.ProfileIds, cancellationToken);
        return NoContent();
    }

    [HttpGet("trips/{tripId:guid}/profiles/{profileId:guid}/packing-list")]
    public async Task<ActionResult<ProfileTripPackingPlan>> GetPackingListAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var plan = await profilePackingLists.GetOrCreateAsync(user.Id, tripId, profileId, cancellationToken);
        return plan is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Perfil o viaje no encontrado")
            : Ok(plan);
    }
}
