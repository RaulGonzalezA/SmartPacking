using SmartPacking.Domain;
using SmartPacking.Contracts;
using SmartPacking.Application;

namespace SmartPacking.Api.Contracts;

public static class TripMapper
{
    public static TripResponse ToResponse(Trip trip) => new(
        trip.Id,
        trip.Destination,
        trip.StartDate,
        trip.EndDate,
        trip.MinimumTemperatureCelsius,
        trip.MaximumTemperatureCelsius,
        trip.Activities.Select(activity => (int)activity).ToArray(),
        trip.TemplateKey,
        trip.LuggageAllowanceGrams,
        trip.CabinOnly,
        (int)trip.LuggageType,
        trip.LuggageHeightCentimetres,
        trip.LuggageWidthCentimetres,
        trip.LuggageDepthCentimetres,
        trip.DayPlansOrEmpty.Select(plan => new TripDayPlanContract(plan.Date, plan.Activities.Select(activity => (int)activity).ToArray())).ToArray(),
        trip.AirlineCode,
        trip.TransportTypesOrEmpty.Select(type => (int)type).ToArray(),
        trip.LuggagesOrDefault.Select(luggage => new TripLuggageContract(luggage.Id, (int)luggage.Type, luggage.AllowanceGrams, luggage.HeightCentimetres, luggage.WidthCentimetres, luggage.DepthCentimetres, luggage.Name)).ToArray(),
        trip.Origin,
        trip.TransportPlan is null
            ? null
            : new TransportPlanContract(
                trip.TransportPlan.Summary,
                trip.TransportPlan.Legs.Select(leg => new TransportLegContract((int)leg.Type, leg.From, leg.To, leg.EstimatedMinutes, leg.Description)).ToArray()));
}

public static class TripFactory
{
    public static Trip CreateForCreation(SaveTripRequest request, Guid id, TripTemplate? template) =>
        Create(request, id, template, template?.Key);

    public static Trip CreateUpdate(SaveTripRequest request, Guid id) =>
        Create(request, id, null, request.TemplateKey);

    private static Trip Create(SaveTripRequest request, Guid id, TripTemplate? template, string? templateKey)
    {
        var cabinOnly = request.CabinOnly ?? template?.CabinOnly ?? true;
        var trip = new Trip(
            id,
            request.Destination.Trim(),
            request.StartDate,
            request.EndDate,
            request.MinimumTemperatureCelsius,
            request.MaximumTemperatureCelsius,
            request.Activities.Count == 0 ? template?.Activities ?? [Style.Casual] : request.Activities.Select(activity => (Style)activity).ToArray(),
            templateKey,
            request.LuggageAllowanceGrams ?? template?.DefaultLuggageAllowanceGrams ?? 10_000,
            cabinOnly,
            (LuggageType)(request.LuggageType ?? (int)(cabinOnly ? LuggageType.Cabin : LuggageType.Checked)),
            request.LuggageHeightCentimetres ?? 55,
            request.LuggageWidthCentimetres ?? 40,
            request.LuggageDepthCentimetres ?? 20,
            request.DayPlans?.Select(plan => new TripDayPlan(plan.Date, plan.Activities.Select(activity => (TripActivity)activity).ToArray())).ToArray(),
            request.AirlineCode,
            request.TransportTypes?.Select(type => (TransportType)type).ToArray(),
            ToLuggages(request.Luggages),
            request.Origin?.Trim());

        return trip with { TransportPlan = TransportPlanner.Build(trip.Origin, trip.Destination, trip.TransportTypesOrEmpty) };
    }

    private static TripLuggage[]? ToLuggages(IReadOnlyCollection<TripLuggageContract>? luggages) => luggages?
        .Select(luggage => new TripLuggage(
            luggage.Id == Guid.Empty ? Guid.NewGuid() : luggage.Id,
            (LuggageType)luggage.Type,
            luggage.AllowanceGrams,
            luggage.HeightCentimetres,
            luggage.WidthCentimetres,
            luggage.DepthCentimetres,
            luggage.Name))
        .ToArray();
}
