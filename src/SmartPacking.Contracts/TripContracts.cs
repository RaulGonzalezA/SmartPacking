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
    bool CabinOnly,
    int LuggageType = 1,
    int LuggageHeightCentimetres = 55,
    int LuggageWidthCentimetres = 40,
    int LuggageDepthCentimetres = 20,
    IReadOnlyCollection<TripDayPlanContract>? DayPlans = null,
    string? AirlineCode = null,
    IReadOnlyCollection<int>? TransportTypes = null,
    IReadOnlyCollection<TripLuggageContract>? Luggages = null);

public sealed record SaveTripRequest(
    string Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    int MinimumTemperatureCelsius,
    int MaximumTemperatureCelsius,
    IReadOnlyCollection<int> Activities,
    string? TemplateKey,
    int? LuggageAllowanceGrams,
    bool? CabinOnly,
    int? LuggageType = null,
    int? LuggageHeightCentimetres = null,
    int? LuggageWidthCentimetres = null,
    int? LuggageDepthCentimetres = null,
    IReadOnlyCollection<TripDayPlanContract>? DayPlans = null,
    string? AirlineCode = null,
    IReadOnlyCollection<int>? TransportTypes = null,
    IReadOnlyCollection<TripLuggageContract>? Luggages = null);

public sealed record TripDayPlanContract(DateOnly Date, IReadOnlyCollection<int> Activities);
public sealed record TripLuggageContract(Guid Id, int Type, int AllowanceGrams, int HeightCentimetres, int WidthCentimetres, int DepthCentimetres, string? Name = null);

public sealed record ChecklistItemResponse(Guid Id, Guid TripId, Guid? ProfileId, int Category, string Name, bool IsPacked);
