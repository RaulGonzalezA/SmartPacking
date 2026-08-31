using System.Text.Json;
using System.Net.Http.Json;

namespace SmartPacking.Infrastructure;

public sealed record WeatherForecast(string Destination, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability, DateOnly StartDate, DateOnly EndDate);

public sealed class OpenMeteoWeatherProvider(HttpClient httpClient)
{
    public async Task<WeatherForecast?> GetAsync(string destination, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var utcNow = DateOnly.FromDateTime(DateTime.UtcNow);
        var maxDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(16));
        if (start < utcNow || start > maxDate) return null;
        if (end < start || end > maxDate) return null;

        var location = await httpClient.GetFromJsonAsync<GeocodingResponse>($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(destination)}&count=1", cancellationToken);
        var match = location?.Results?.FirstOrDefault(); if (match is null) return null;

        var lat = match.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = match.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&daily=temperature_2m_min,temperature_2m_max,precipitation_probability_max&timezone=auto&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}";

        var forecast = await httpClient.GetFromJsonAsync<ForecastResponse>(url, cancellationToken);
        if (forecast?.Daily?.Minimum is null || forecast.Daily.Maximum is null || forecast.Daily.Minimum.Length == 0) return null;
        return new WeatherForecast(destination, forecast.Daily.Minimum.Min(), forecast.Daily.Maximum.Max(), forecast.Daily.RainProbability?.Max() ?? 0, start, end);
    }
    private sealed record GeocodingResponse(GeocodingResult[]? Results);
    private sealed record GeocodingResult(decimal Latitude, decimal Longitude);
    private sealed record ForecastResponse(DailyForecast? Daily);
    private sealed record DailyForecast(decimal[]? Minimum, decimal[]? Maximum, int[]? RainProbability);
}
