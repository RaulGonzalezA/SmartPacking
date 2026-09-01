using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace SmartPacking.Infrastructure;

public sealed record DailyWeatherForecast(DateOnly Date, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability, int WeatherCode, decimal? ApparentMinimumCelsius = null, decimal? ApparentMaximumCelsius = null, decimal? WindSpeedKilometresPerHour = null);
public sealed record WeatherForecast(string Destination, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<DailyWeatherForecast> Daily);

public sealed partial class OpenMeteoWeatherProvider(HttpClient httpClient, IDistributedCache cache, ILogger<OpenMeteoWeatherProvider> logger)
{
    public async Task<WeatherForecast?> GetAsync(string destination, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destination) || end < start)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Open-Meteo exposes 16 calendar days counting today, hence today + 15.
        var lastForecastDate = today.AddDays(15);
        if (end < today || start > lastForecastDate)
        {
            return null;
        }

        var forecastStart = start < today ? today : start;
        var forecastEnd = end > lastForecastDate ? lastForecastDate : end;
        var cacheKey = $"weather:v3:{destination.Trim().ToUpperInvariant()}:{forecastStart:yyyyMMdd}:{forecastEnd:yyyyMMdd}";
        var cachedForecast = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedForecast))
        {
            return JsonSerializer.Deserialize<WeatherForecast>(cachedForecast);
        }

        try
        {
            var location = await httpClient.GetFromJsonAsync<GeocodingResponse>($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(destination)}&count=1", cancellationToken);
            var match = location?.Results?.FirstOrDefault();
            if (match is null)
            {
                return null;
            }

            var latitude = match.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var longitude = match.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&daily=temperature_2m_min,temperature_2m_max,apparent_temperature_min,apparent_temperature_max,precipitation_probability_max,weather_code,wind_speed_10m_max&timezone=auto&start_date={forecastStart:yyyy-MM-dd}&end_date={forecastEnd:yyyy-MM-dd}";
            var forecast = await httpClient.GetFromJsonAsync<ForecastResponse>(url, cancellationToken);
            var dailyForecast = forecast?.Daily;
            if (dailyForecast is not { Dates: { Length: > 0 } dates, Minimum: { Length: > 0 } minimum, Maximum: { Length: > 0 } maximum }
                || dates.Length != minimum.Length
                || minimum.Length != maximum.Length)
            {
                return null;
            }

            var daily = Enumerable.Range(0, minimum.Length)
                .Select(index => new DailyWeatherForecast(
                    DateOnly.Parse(dates[index], System.Globalization.CultureInfo.InvariantCulture),
                    minimum[index],
                    maximum[index],
                    dailyForecast.RainProbability?.ElementAtOrDefault(index) ?? 0,
                    dailyForecast.WeatherCode?.ElementAtOrDefault(index) ?? 0,
                    dailyForecast.ApparentMinimum?.ElementAtOrDefault(index),
                    dailyForecast.ApparentMaximum?.ElementAtOrDefault(index),
                    dailyForecast.WindSpeed?.ElementAtOrDefault(index)))
                .ToArray();
            var result = new WeatherForecast(destination.Trim(), daily.Min(day => day.MinimumCelsius), daily.Max(day => day.MaximumCelsius), daily.Max(day => day.RainProbability), forecastStart, forecastEnd, daily);
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3) }, cancellationToken);
            return result;
        }
        catch (HttpRequestException exception)
        {
            LogNoResponse(logger, exception, destination);
            return null;
        }
        catch (JsonException exception)
        {
            LogInvalidResponse(logger, exception, destination);
            return null;
        }
    }

    [LoggerMessage(LogLevel.Warning, "Open-Meteo no respondió para {Destination}")]
    private static partial void LogNoResponse(ILogger logger, Exception exception, string destination);

    [LoggerMessage(LogLevel.Warning, "Open-Meteo devolvió una previsión inválida para {Destination}")]
    private static partial void LogInvalidResponse(ILogger logger, Exception exception, string destination);

    private sealed record GeocodingResponse(GeocodingResult[]? Results);
    private sealed record GeocodingResult(decimal Latitude, decimal Longitude);
    private sealed record ForecastResponse(DailyForecast? Daily);
    private sealed record DailyForecast(
        [property: JsonPropertyName("time")] string[]? Dates,
        [property: JsonPropertyName("temperature_2m_min")] decimal[]? Minimum,
        [property: JsonPropertyName("temperature_2m_max")] decimal[]? Maximum,
        [property: JsonPropertyName("apparent_temperature_min")] decimal[]? ApparentMinimum,
        [property: JsonPropertyName("apparent_temperature_max")] decimal[]? ApparentMaximum,
        [property: JsonPropertyName("precipitation_probability_max")] int[]? RainProbability,
        [property: JsonPropertyName("weather_code")] int[]? WeatherCode,
        [property: JsonPropertyName("wind_speed_10m_max")] decimal[]? WindSpeed);
}
