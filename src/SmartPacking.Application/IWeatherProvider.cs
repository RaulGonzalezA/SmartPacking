namespace SmartPacking.Application;

public interface IWeatherProvider
{
    Task<IReadOnlyList<CitySuggestion>> SearchCitiesAsync(string query, CancellationToken cancellationToken);
    Task<WeatherForecast?> GetAsync(string destination, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
}

public sealed record DailyWeatherForecast(
    DateOnly Date,
    decimal MinimumCelsius,
    decimal MaximumCelsius,
    int RainProbability,
    int WeatherCode,
    decimal? ApparentMinimumCelsius = null,
    decimal? ApparentMaximumCelsius = null,
    decimal? WindSpeedKilometresPerHour = null);

public sealed record WeatherForecast(
    string Destination,
    decimal MinimumCelsius,
    decimal MaximumCelsius,
    int RainProbability,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<DailyWeatherForecast> Daily);
