using SmartPacking.Domain;

namespace SmartPacking.Application;

public sealed record TripTemplate(
    string Key,
    string Name,
    string Description,
    IReadOnlyCollection<Style> Activities,
    int DefaultMinimumTemperatureCelsius,
    int DefaultMaximumTemperatureCelsius,
    int DefaultLuggageAllowanceGrams,
    bool CabinOnly);

public sealed record UserTripTemplate(Guid Id, Guid UserId, string Name, string Description, IReadOnlyCollection<Style> Activities, int MinimumTemperatureCelsius, int MaximumTemperatureCelsius, int LuggageAllowanceGrams, bool CabinOnly);

public static class TripTemplateCatalog
{
    public static readonly IReadOnlyList<TripTemplate> All =
    [
        new("city-break", "Escapada urbana", "Equipaje de cabina para unos días de turismo y cenas.", [Style.Casual], 15, 25, 10000, true),
        new("beach", "Playa", "Prendas ligeras, calzado abierto y protección solar.", [Style.Casual, Style.Sport], 22, 32, 10000, true),
        new("business", "Trabajo", "Ropa formal y tecnología para reuniones.", [Style.Formal], 12, 22, 10000, true),
        new("outdoor", "Naturaleza", "Ropa deportiva, capas y protección frente a la lluvia.", [Style.Sport], 5, 18, 15000, false)
    ];

    public static TripTemplate? Find(string? key) =>
        All.SingleOrDefault(template => string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase));
}

public sealed record LuggageRulesSummary(
    int AllowanceGrams,
    int PlannedWeightGrams,
    int RemainingGrams,
    bool CabinOnly,
    bool IsWithinAllowance,
    int LiquidContainerMaximumMilliliters,
    int CabinLiquidsBagMaximumMilliliters);

public sealed record DailyTripForecast(DateOnly Date, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability, int WeatherCode, decimal? ApparentMinimumCelsius = null, decimal? ApparentMaximumCelsius = null, decimal? WindSpeedKilometresPerHour = null);
public sealed record TripWeatherForecast(string Destination, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<DailyTripForecast> Daily);
