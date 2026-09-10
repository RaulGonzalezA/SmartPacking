using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SmartPacking.Api;
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
    OpenMeteoWeatherProvider weather,
    IValidator<SaveTripRequest> tripValidator,
    IValidator<SaveUserTripTemplateRequest> templateValidator,
    IValidator<CreateChecklistItemRequest> checklistValidator) : ControllerBase
{
    private const string viajeNoEncontrado = "Viaje no encontrado";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        return Ok((await store.GetTripsAsync(user.Id, cancellationToken)).Select(ToResponse).ToArray());
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
            request.CabinOnly ?? template?.CabinOnly ?? true,
            (LuggageType)(request.LuggageType ?? (int)(request.CabinOnly ?? template?.CabinOnly ?? true ? LuggageType.Cabin : LuggageType.Checked)),
            request.LuggageHeightCentimetres ?? 55,
            request.LuggageWidthCentimetres ?? 40,
            request.LuggageDepthCentimetres ?? 20,
            request.DayPlans?.Select(plan => new TripDayPlan(plan.Date, plan.Activities.Select(activity => (TripActivity)activity).ToArray())).ToArray(), request.AirlineCode,
            request.TransportTypes?.Select(type => (TransportType)type).ToArray(), ToLuggages(request.Luggages), request.Origin?.Trim());
        trip = trip with { TransportPlan = TransportPlanner.Build(trip.Origin, trip.Destination, trip.TransportTypesOrEmpty) };
        var created = await store.AddTripAsync(user.Id, trip, cancellationToken);
        await store.SetTripProfilesAsync(user.Id, created.Id, [user.Id], cancellationToken);
        await store.AddChecklistItemsAsync(user.Id, ChecklistDefaults.Create(created.Id), cancellationToken);
        return Created($"/api/trips/{created.Id}", ToResponse(created));
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
        var trip = new Trip(tripId, request.Destination.Trim(), request.StartDate, request.EndDate, request.MinimumTemperatureCelsius, request.MaximumTemperatureCelsius, request.Activities.Count == 0 ? [Style.Casual] : request.Activities.Select(activity => (Style)activity).ToArray(), request.TemplateKey, request.LuggageAllowanceGrams ?? 10000, request.CabinOnly ?? true, (LuggageType)(request.LuggageType ?? (int)(request.CabinOnly ?? true ? LuggageType.Cabin : LuggageType.Checked)), request.LuggageHeightCentimetres ?? 55, request.LuggageWidthCentimetres ?? 40, request.LuggageDepthCentimetres ?? 20, request.DayPlans?.Select(plan => new TripDayPlan(plan.Date, plan.Activities.Select(activity => (TripActivity)activity).ToArray())).ToArray(), request.AirlineCode, request.TransportTypes?.Select(type => (TransportType)type).ToArray(), ToLuggages(request.Luggages), request.Origin?.Trim());
        trip = trip with { TransportPlan = TransportPlanner.Build(trip.Origin, trip.Destination, trip.TransportTypesOrEmpty) };
        var updated = await store.UpdateTripAsync(user.Id, trip, cancellationToken);
        return updated is null ? NotFoundProblem(viajeNoEncontrado) : Ok(ToResponse(updated));
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
        var trip = await store.GetTripAsync(user.Id, tripId, cancellationToken);
        if (trip is null)
        {
            return NotFoundProblem(viajeNoEncontrado);
        }

        var (forecast, weatherFeedback) = await GetDashboardWeatherAsync(trip, cancellationToken);
        var profiles = await store.GetTripProfilesAsync(user.Id, tripId, cancellationToken);
        var selectedProfileId = Guid.Empty;
        if (profiles.Any(profile => profile.Id == profileId))
        {
            selectedProfileId = profileId!.Value;
        }
        else if (profiles.Count > 0)
        {
            selectedProfileId = profiles[0].Id;
        }
        var familyPlans = new List<ProfileTripPackingPlan>();
        var progress = new List<PreparationProgressItem>();
        IReadOnlyList<ChecklistItem> selectedChecklist = [];

        foreach (var profile in profiles)
        {
            var plan = await profilePackingLists.GetOrCreateAsync(user.Id, tripId, profile.Id, cancellationToken, forecast);
            var checklist = await GetOrCreateProfileChecklistAsync(user.Id, tripId, profile.Id, cancellationToken);
            if (plan is not null)
            {
                familyPlans.Add(plan);
            }

            progress.Add(new PreparationProgressItem(
                profile.Name,
                plan?.Plan.Items.Count(item => item.IsPacked) ?? 0,
                plan?.Plan.Items.Count ?? 0,
                checklist.Count(item => item.IsPacked),
                checklist.Count));
            if (profile.Id == selectedProfileId)
            {
                selectedChecklist = checklist;
            }
        }

        var selectedPlan = familyPlans.SingleOrDefault(plan => plan.Profile.Id == selectedProfileId);
        var rules = selectedPlan is null ? null : BuildLuggageRules(trip, selectedPlan);
        var usage = await store.GetUsageAsync(user.Id, tripId, cancellationToken);
        return Ok(new TripDashboard(profiles, selectedProfileId, selectedPlan, familyPlans, selectedChecklist, progress, rules, usage, forecast, weatherFeedback));
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

    private static TripResponse ToResponse(Trip trip) => new(trip.Id, trip.Destination, trip.StartDate, trip.EndDate, trip.MinimumTemperatureCelsius, trip.MaximumTemperatureCelsius, trip.Activities.Select(activity => (int)activity).ToArray(), trip.TemplateKey, trip.LuggageAllowanceGrams, trip.CabinOnly, (int)trip.LuggageType, trip.LuggageHeightCentimetres, trip.LuggageWidthCentimetres, trip.LuggageDepthCentimetres, trip.DayPlansOrEmpty.Select(plan => new TripDayPlanContract(plan.Date, plan.Activities.Select(activity => (int)activity).ToArray())).ToArray(), trip.AirlineCode, trip.TransportTypesOrEmpty.Select(type => (int)type).ToArray(), trip.LuggagesOrDefault.Select(luggage => new TripLuggageContract(luggage.Id, (int)luggage.Type, luggage.AllowanceGrams, luggage.HeightCentimetres, luggage.WidthCentimetres, luggage.DepthCentimetres, luggage.Name)).ToArray(), trip.Origin, ToTransportPlan(trip.TransportPlan));
    private static TransportPlanContract? ToTransportPlan(TransportPlan? plan) => plan is null ? null : new(plan.Summary, plan.Legs.Select(leg => new TransportLegContract((int)leg.Type, leg.From, leg.To, leg.EstimatedMinutes, leg.Description)).ToArray());
    private static TripLuggage[]? ToLuggages(IReadOnlyCollection<TripLuggageContract>? luggages) => luggages?.Select(luggage => new TripLuggage(luggage.Id == Guid.Empty ? Guid.NewGuid() : luggage.Id, (LuggageType)luggage.Type, luggage.AllowanceGrams, luggage.HeightCentimetres, luggage.WidthCentimetres, luggage.DepthCentimetres, luggage.Name)).ToArray();
    private async Task<IReadOnlyList<ChecklistItem>> GetOrCreateProfileChecklistAsync(Guid userId, Guid tripId, Guid profileId, CancellationToken cancellationToken)
    {
        var items = await store.GetChecklistAsync(userId, tripId, profileId, cancellationToken);
        return items.Count == 0
            ? await store.AddChecklistItemsAsync(userId, ChecklistDefaults.Create(tripId, profileId), cancellationToken)
            : items;
    }

    private async Task<(TripWeatherForecast? Forecast, string? Feedback)> GetDashboardWeatherAsync(Trip trip, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (trip.EndDate < today)
        {
            return (null, "La previsión no está disponible para viajes ya finalizados.");
        }

        if (trip.StartDate > today.AddDays(15))
        {
            return (null, $"La previsión detallada estará disponible a partir del {trip.StartDate.AddDays(-15).ToString("d", System.Globalization.CultureInfo.CurrentCulture)}.");
        }

        var forecast = await weather.GetAsync(trip.Destination, trip.StartDate, trip.EndDate, cancellationToken);
        return forecast is null
            ? (null, "No se ha podido obtener la previsión para este destino en este momento.")
            : (new TripWeatherForecast(
                forecast.Destination,
                forecast.MinimumCelsius,
                forecast.MaximumCelsius,
                forecast.RainProbability,
                forecast.StartDate,
                forecast.EndDate,
                forecast.Daily.Select(day => new DailyTripForecast(day.Date, day.MinimumCelsius, day.MaximumCelsius, day.RainProbability, day.WeatherCode, day.ApparentMinimumCelsius, day.ApparentMaximumCelsius, day.WindSpeedKilometresPerHour)).ToArray()), null);
    }

    private static LuggageRulesSummary BuildLuggageRules(Trip trip, ProfileTripPackingPlan profilePlan)
    {
        var weight = profilePlan.Plan.TotalWeightGrams;
        var remaining = trip.LuggageAllowanceGrams - weight;
        var plannedVolume = profilePlan.Plan.Items.Sum(item => EstimatedVolumeMillilitres(item.Recommendation.Item.Type));
        var capacityVolume = trip.LuggageHeightCentimetres * trip.LuggageWidthCentimetres * trip.LuggageDepthCentimetres * 1000;
        return new LuggageRulesSummary(trip.LuggageAllowanceGrams, weight, remaining, trip.CabinOnly, remaining >= 0, 100, 1000, plannedVolume, capacityVolume);
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
