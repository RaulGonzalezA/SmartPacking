namespace SmartPacking.Contracts;

public sealed record TripResponse(
    Guid Id,
    string Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    int MinimumTemperatureCelsius,
    int MaximumTemperatureCelsius,
    IReadOnlyCollection<int> Activities,
    string? TemplateKey,
    int LuggageAllowanceGrams,
    bool CabinOnly);

public sealed record SaveTripRequest(
    string Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    int MinimumTemperatureCelsius,
    int MaximumTemperatureCelsius,
    IReadOnlyCollection<int> Activities,
    string? TemplateKey,
    int? LuggageAllowanceGrams,
    bool? CabinOnly);

public sealed record ChecklistItemResponse(Guid Id, Guid TripId, Guid? ProfileId, int Category, string Name, bool IsPacked);
