using Microsoft.AspNetCore.Mvc;
using SmartPacking.Api;
using SmartPacking.Application;
using SmartPacking.Domain;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/trips")]
public sealed class TripsController(ISmartPackingStore store, PackingListService packingLists, OpenMeteoWeatherProvider weather) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Trip>>> GetAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok(await store.GetTripsAsync(user.Id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<Trip>> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken)
    {
        if (request.EndDate < request.StartDate || string.IsNullOrWhiteSpace(request.Destination))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["trip"] = ["Introduce un destino y fechas válidas."] }));
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var trip = new Trip(Guid.NewGuid(), request.Destination.Trim(), request.StartDate, request.EndDate, request.MinimumTemperatureCelsius, request.MaximumTemperatureCelsius, request.Activities.Count == 0 ? [Style.Casual] : request.Activities);
        var created = await store.AddTripAsync(user.Id, trip, cancellationToken);
        await store.SetTripProfilesAsync(user.Id, created.Id, [user.Id], cancellationToken);
        await store.AddChecklistItemsAsync(user.Id, ChecklistDefaults.Create(created.Id), cancellationToken);
        return Created($"/api/trips/{created.Id}", created);
    }

    [HttpDelete("{tripId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return await store.DeleteTripAsync(user.Id, tripId, cancellationToken) ? NoContent() : NotFoundProblem("Viaje no encontrado");
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

        var forecast = await weather.GetAsync(trip.Destination, trip.StartDate, trip.EndDate, cancellationToken);
        return forecast is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Previsión no disponible", detail: "La previsión solo está disponible para los próximos 16 días.")
            : Ok(forecast);
    }

    [HttpGet("{tripId:guid}/checklist")]
    public async Task<ActionResult<IReadOnlyList<ChecklistItem>>> GetChecklistAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null)
        {
            return NotFoundProblem("Viaje no encontrado");
        }

        var items = await store.GetChecklistAsync(user.Id, tripId, cancellationToken);
        return Ok(items.Count == 0 ? await store.AddChecklistItemsAsync(user.Id, ChecklistDefaults.Create(tripId), cancellationToken) : items);
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

    private ObjectResult NotFoundProblem(string title) => Problem(statusCode: StatusCodes.Status404NotFound, title: title);
}

internal static class ChecklistDefaults
{
    public static IReadOnlyList<ChecklistItem> Create(Guid tripId) =>
    [
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "DNI o pasaporte", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "Tarjetas y reservas", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "Seguro de viaje", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Cepillo y pasta de dientes", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Desodorante", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Protector solar", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Móvil y cargador", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Adaptador de enchufe", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Auriculares", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Health, "Medicación personal", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Health, "Tiritas y básicos", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Other, "Gafas de sol", false),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Other, "Botella reutilizable", false)
    ];
}
