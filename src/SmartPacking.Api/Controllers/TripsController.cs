using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SmartPacking.Api;
using SmartPacking.Api.Contracts;
using SmartPacking.Api.Validation;
using SmartPacking.Application;
using SmartPacking.Contracts;
using SmartPacking.Domain;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/trips")]
public sealed class TripsController(
    ISmartPackingStore store,
    PackingListService packingLists,
    ProfilePackingListService profilePackingLists,
    TripDashboardService dashboards,
    IWeatherProvider weather,
    IValidator<SaveTripRequest> tripValidator,
    IValidator<SaveUserTripTemplateRequest> templateValidator,
    IValidator<CreateChecklistItemRequest> checklistValidator) : ControllerBase
{
    private const string viajeNoEncontrado = "Viaje no encontrado";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok((await store.GetTripsAsync(user.Id, cancellationToken)).Select(TripMapper.ToResponse).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<TripResponse>> CreateAsync(SaveTripRequest request, CancellationToken cancellationToken)
    {
        var validationProblem = await tripValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var template = TripTemplateCatalog.Find(request.TemplateKey);
        var trip = await ResolveCoordinatesAsync(TripFactory.CreateForCreation(request, Guid.NewGuid(), template), cancellationToken);
        var created = await store.AddTripAsync(user.Id, trip, cancellationToken);
        await store.SetTripProfilesAsync(user.Id, created.Id, [user.Id], cancellationToken);
        await store.AddChecklistItemsAsync(user.Id, PackingChecklistDefaults.Create(created.Id), cancellationToken);
        return Created($"/api/trips/{created.Id}", TripMapper.ToResponse(created));
    }

    [HttpDelete("{tripId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.DeleteTripAsync(user.Id, tripId, cancellationToken) ? NoContent() : NotFoundProblem(viajeNoEncontrado);
    }

    [HttpPut("{tripId:guid}")]
    public async Task<ActionResult<TripResponse>> UpdateAsync(Guid tripId, SaveTripRequest request, CancellationToken cancellationToken)
    {
        var validationProblem = await tripValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var trip = await ResolveCoordinatesAsync(TripFactory.CreateUpdate(request, tripId), cancellationToken);
        var updated = await store.UpdateTripAsync(user.Id, trip, cancellationToken);
        return updated is null ? NotFoundProblem(viajeNoEncontrado) : Ok(TripMapper.ToResponse(updated));
    }

    [HttpGet("{tripId:guid}/packing-list")]
    public async Task<ActionResult<TripPackingPlan>> GetPackingListAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var plan = await packingLists.GetOrCreateAsync(user.Id, tripId, cancellationToken);
        return plan is null ? NotFoundProblem(viajeNoEncontrado) : Ok(plan);
    }

    [HttpGet("{tripId:guid}/dashboard")]
    [ProducesResponseType(typeof(TripDashboard), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDashboard>> GetDashboardAsync(Guid tripId, [FromQuery] Guid? profileId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var dashboard = await dashboards.GetAsync(user.Id, tripId, profileId, cancellationToken);
        return dashboard is null ? NotFoundProblem(viajeNoEncontrado) : Ok(dashboard);
    }

    [HttpGet("{tripId:guid}/weather")]
    public async Task<ActionResult<WeatherForecast>> GetWeatherAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var trip = await store.GetTripAsync(user.Id, tripId, cancellationToken);
        if (trip is null)
        {
            return NotFoundProblem(viajeNoEncontrado);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastForecastDate = today.AddDays(15);
        if (trip.EndDate < today)
        {
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Viaje finalizado", detail: "La previsión no está disponible para viajes ya finalizados.");
        }

        if (trip.StartDate > lastForecastDate)
        {
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Previsión aún no disponible", detail: $"La previsión detallada estará disponible a partir del {trip.StartDate.AddDays(-15).ToString("d", System.Globalization.CultureInfo.CurrentCulture)}.");
        }

        var forecast = await weather.GetAsync(trip.Destination, trip.StartDate, trip.EndDate, trip.Latitude, trip.Longitude, cancellationToken);
        return forecast is null
            ? Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Previsión no disponible", detail: "No se ha podido obtener la previsión para este destino en este momento.")
            : Ok(forecast);
    }

    [HttpGet("templates")]
    public ActionResult<IReadOnlyList<TripTemplate>> GetTemplates() => Ok(TripTemplateCatalog.All);

    [HttpGet("user-templates")]
    public async Task<ActionResult<IReadOnlyList<UserTripTemplate>>> GetUserTemplatesAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok(await store.GetUserTripTemplatesAsync(user.Id, cancellationToken));
    }

    [HttpPost("user-templates")]
    public async Task<ActionResult<UserTripTemplate>> CreateUserTemplateAsync(SaveUserTripTemplateRequest request, CancellationToken cancellationToken)
    {
        var validationProblem = await templateValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var created = await store.AddUserTripTemplateAsync(new UserTripTemplate(Guid.NewGuid(), user.Id, request.Name.Trim(), request.Description?.Trim() ?? string.Empty, request.Activities, request.MinimumTemperatureCelsius, request.MaximumTemperatureCelsius, request.LuggageAllowanceGrams, request.CabinOnly), cancellationToken);
        return Created($"/api/trips/user-templates/{created.Id}", created);
    }

    [HttpDelete("user-templates/{templateId:guid}")]
    public async Task<IActionResult> DeleteUserTemplateAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.DeleteUserTripTemplateAsync(user.Id, templateId, cancellationToken) ? NoContent() : NotFoundProblem("Plantilla no encontrada");
    }

    [HttpGet("{tripId:guid}/profiles/{profileId:guid}/luggage-rules")]
    public async Task<ActionResult<LuggageRulesSummary>> GetLuggageRulesAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var plan = await packingLists.GetOrCreateAsync(user.Id, tripId, cancellationToken);
        var trip = await store.GetTripAsync(user.Id, tripId, cancellationToken);
        var isTraveller = (await store.GetTripProfilesAsync(user.Id, tripId, cancellationToken)).Any(profile => profile.Id == profileId);
        if (plan is null || trip is null || !isTraveller)
        {
            return NotFoundProblem("Perfil o viaje no encontrado");
        }

        var profilePlan = await profilePackingLists.GetOrCreateAsync(user.Id, tripId, profileId, cancellationToken);
        var weight = profilePlan?.Plan.TotalWeightGrams ?? 0;
        var remaining = trip.LuggageAllowanceGrams - weight;
        var plannedVolume = profilePlan?.Plan.Items.Sum(item => EstimatedVolumeMillilitres(item.Recommendation.Item.Type)) ?? 0;
        var capacityVolume = trip.LuggageHeightCentimetres * trip.LuggageWidthCentimetres * trip.LuggageDepthCentimetres * 1000;
        return Ok(new LuggageRulesSummary(trip.LuggageAllowanceGrams, weight, remaining, trip.CabinOnly, remaining >= 0, 100, 1000, plannedVolume, capacityVolume));
    }

    [HttpGet("{tripId:guid}/checklist")]
    public async Task<ActionResult<IReadOnlyList<ChecklistItem>>> GetChecklistAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null)
        {
            return NotFoundProblem(viajeNoEncontrado);
        }

        var items = await store.GetChecklistAsync(user.Id, tripId, null, cancellationToken);
        return Ok(items.Count == 0 ? await store.AddChecklistItemsAsync(user.Id, PackingChecklistDefaults.Create(tripId), cancellationToken) : items);
    }

    [HttpGet("{tripId:guid}/profiles/{profileId:guid}/checklist")]
    public async Task<ActionResult<IReadOnlyList<ChecklistItem>>> GetProfileChecklistAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        if (!(await store.GetTripProfilesAsync(user.Id, tripId, cancellationToken)).Any(profile => profile.Id == profileId))
        {
            return NotFoundProblem("Perfil o viaje no encontrado");
        }

        return Ok(await GetOrCreateProfileChecklistAsync(user.Id, tripId, profileId, cancellationToken));
    }

    [HttpPost("{tripId:guid}/checklist")]
    public async Task<ActionResult<ChecklistItem>> AddChecklistItemAsync(Guid tripId, CreateChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var validationProblem = await checklistValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null)
        {
            return NotFoundProblem(viajeNoEncontrado);
        }

        var item = new ChecklistItem(Guid.NewGuid(), tripId, request.Category, request.Name.Trim(), false);
        await store.AddChecklistItemsAsync(user.Id, [item], cancellationToken);
        return Created($"/api/trips/{tripId}/checklist/{item.Id}", item);
    }

    [HttpGet("{tripId:guid}/usage")]
    public async Task<ActionResult<IReadOnlyList<ClothingUsage>>> GetUsageAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok(await store.GetUsageAsync(user.Id, tripId, cancellationToken));
    }

    [HttpPost("{tripId:guid}/usage")]
    public async Task<IActionResult> SaveUsageAsync(Guid tripId, IReadOnlyCollection<ClothingUsage> usage, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        await store.SaveUsageAsync(user.Id, tripId, usage, cancellationToken);
        return NoContent();
    }

    private async Task<IReadOnlyList<ChecklistItem>> GetOrCreateProfileChecklistAsync(Guid userId, Guid tripId, Guid profileId, CancellationToken cancellationToken)
    {
        var items = await store.GetChecklistAsync(userId, tripId, profileId, cancellationToken);
        return items.Count == 0
            ? await store.AddChecklistItemsAsync(userId, PackingChecklistDefaults.Create(tripId, profileId), cancellationToken)
            : items;
    }

    private async Task<Trip> ResolveCoordinatesAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (trip.Latitude is not null && trip.Longitude is not null)
        {
            return trip;
        }

        var destination = trip.Destination.Split(',', 2)[0].Trim();
        var cities = await weather.SearchCitiesAsync(destination, cancellationToken);
        var city = cities.FirstOrDefault(candidate => string.Equals(candidate.Name, destination, StringComparison.OrdinalIgnoreCase))
            ?? (cities.Count > 0 ? cities[0] : null);
        return city?.Latitude is not null && city.Longitude is not null
            ? trip with { Latitude = city.Latitude, Longitude = city.Longitude }
            : trip;
    }

    private static int EstimatedVolumeMillilitres(ClothingType type) => type switch
    {
        ClothingType.Jacket => 7000,
        ClothingType.Shoes => 6000,
        ClothingType.Trousers => 2500,
        ClothingType.Shorts => 1200,
        ClothingType.TShirt => 900,
        ClothingType.Sandals => 2500,
        _ => 500
    };

    private ObjectResult NotFoundProblem(string title) => Problem(statusCode: StatusCodes.Status404NotFound, title: title);
}
