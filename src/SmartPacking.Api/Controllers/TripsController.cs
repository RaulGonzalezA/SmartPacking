using Microsoft.AspNetCore.Mvc;
using SmartPacking.Api;
using SmartPacking.Application;
using SmartPacking.Contracts;
using SmartPacking.Domain;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/trips")]
public sealed class TripsController(ISmartPackingStore store, PackingListService packingLists, ProfilePackingListService profilePackingLists, OpenMeteoWeatherProvider weather) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok((await store.GetTripsAsync(user.Id, cancellationToken)).Select(ToResponse).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<TripResponse>> CreateAsync(SaveTripRequest request, CancellationToken cancellationToken)
    {
        if (request.EndDate < request.StartDate || string.IsNullOrWhiteSpace(request.Destination))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["trip"] = ["Introduce un destino y fechas válidas."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var template = TripTemplateCatalog.Find(request.TemplateKey);
        var trip = new Trip(
            Guid.NewGuid(),
            request.Destination.Trim(),
            request.StartDate,
            request.EndDate,
            request.MinimumTemperatureCelsius,
            request.MaximumTemperatureCelsius,
            request.Activities.Count == 0 ? template?.Activities ?? [Style.Casual] : request.Activities.Select(activity => (Style)activity).ToArray(),
            template?.Key,
            request.LuggageAllowanceGrams ?? template?.DefaultLuggageAllowanceGrams ?? 10000,
            request.CabinOnly ?? template?.CabinOnly ?? true);
        var created = await store.AddTripAsync(user.Id, trip, cancellationToken);
        await store.SetTripProfilesAsync(user.Id, created.Id, [user.Id], cancellationToken);
        await store.AddChecklistItemsAsync(user.Id, ChecklistDefaults.Create(created.Id), cancellationToken);
        return Created($"/api/trips/{created.Id}", ToResponse(created));
    }

    [HttpDelete("{tripId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.DeleteTripAsync(user.Id, tripId, cancellationToken) ? NoContent() : NotFoundProblem("Viaje no encontrado");
    }

    [HttpPut("{tripId:guid}")]
    public async Task<ActionResult<TripResponse>> UpdateAsync(Guid tripId, SaveTripRequest request, CancellationToken cancellationToken)
    {
        if (request.EndDate < request.StartDate || string.IsNullOrWhiteSpace(request.Destination) || request.LuggageAllowanceGrams is < 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["trip"] = ["Introduce datos válidos para el viaje."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var trip = new Trip(tripId, request.Destination.Trim(), request.StartDate, request.EndDate, request.MinimumTemperatureCelsius, request.MaximumTemperatureCelsius, request.Activities.Count == 0 ? [Style.Casual] : request.Activities.Select(activity => (Style)activity).ToArray(), request.TemplateKey, request.LuggageAllowanceGrams ?? 10000, request.CabinOnly ?? true);
        var updated = await store.UpdateTripAsync(user.Id, trip, cancellationToken);
        return updated is null ? NotFoundProblem("Viaje no encontrado") : Ok(ToResponse(updated));
    }

    [HttpGet("{tripId:guid}/packing-list")]
    public async Task<ActionResult<TripPackingPlan>> GetPackingListAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var plan = await packingLists.GetOrCreateAsync(user.Id, tripId, cancellationToken);
        return plan is null ? NotFoundProblem("Viaje no encontrado") : Ok(plan);
    }

    [HttpGet("{tripId:guid}/weather")]
    public async Task<ActionResult<WeatherForecast>> GetWeatherAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var trip = await store.GetTripAsync(user.Id, tripId, cancellationToken);
        if (trip is null)
        {
            return NotFoundProblem("Viaje no encontrado");
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

        var forecast = await weather.GetAsync(trip.Destination, trip.StartDate, trip.EndDate, cancellationToken);
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
        if (string.IsNullOrWhiteSpace(request.Name) || request.MaximumTemperatureCelsius < request.MinimumTemperatureCelsius || request.LuggageAllowanceGrams < 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["template"] = ["Introduce una plantilla válida."] }));
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
        return Ok(new LuggageRulesSummary(trip.LuggageAllowanceGrams, weight, remaining, trip.CabinOnly, remaining >= 0, 100, 1000));
    }

    [HttpGet("{tripId:guid}/checklist")]
    public async Task<ActionResult<IReadOnlyList<ChecklistItem>>> GetChecklistAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null)
        {
            return NotFoundProblem("Viaje no encontrado");
        }

        var items = await store.GetChecklistAsync(user.Id, tripId, null, cancellationToken);
        return Ok(items.Count == 0 ? await store.AddChecklistItemsAsync(user.Id, ChecklistDefaults.Create(tripId), cancellationToken) : items);
    }

    [HttpGet("{tripId:guid}/profiles/{profileId:guid}/checklist")]
    public async Task<ActionResult<IReadOnlyList<ChecklistItem>>> GetProfileChecklistAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        if (!(await store.GetTripProfilesAsync(user.Id, tripId, cancellationToken)).Any(profile => profile.Id == profileId))
        {
            return NotFoundProblem("Perfil o viaje no encontrado");
        }

        var items = await store.GetChecklistAsync(user.Id, tripId, profileId, cancellationToken);
        return Ok(items.Count == 0 ? await store.AddChecklistItemsAsync(user.Id, ChecklistDefaults.Create(tripId, profileId), cancellationToken) : items);
    }

    [HttpPost("{tripId:guid}/checklist")]
    public async Task<ActionResult<ChecklistItem>> AddChecklistItemAsync(Guid tripId, CreateChecklistItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = ["Escribe un elemento para la checklist."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null)
        {
            return NotFoundProblem("Viaje no encontrado");
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

    private static TripResponse ToResponse(Trip trip) => new(trip.Id, trip.Destination, trip.StartDate, trip.EndDate, trip.MinimumTemperatureCelsius, trip.MaximumTemperatureCelsius, trip.Activities.Select(activity => (int)activity).ToArray(), trip.TemplateKey, trip.LuggageAllowanceGrams, trip.CabinOnly);
    private ObjectResult NotFoundProblem(string title) => Problem(statusCode: StatusCodes.Status404NotFound, title: title);
}

internal static class ChecklistDefaults
{
    public static IReadOnlyList<ChecklistItem> Create(Guid tripId, Guid? profileId = null) =>
    [
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "DNI o pasaporte", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "Tarjetas y reservas", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "Seguro de viaje", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Cepillo y pasta de dientes", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Desodorante", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Protector solar", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Móvil y cargador", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Adaptador de enchufe", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Auriculares", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Health, "Medicación personal", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Health, "Tiritas y básicos", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Other, "Gafas de sol", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Other, "Botella reutilizable", false, profileId)
    ];
}
