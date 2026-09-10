using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SmartPacking.Application;

namespace SmartPacking.Infrastructure;

public sealed partial class OpenMeteoWeatherProvider(HttpClient httpClient, IDistributedCache cache, ILogger<OpenMeteoWeatherProvider> logger) : IWeatherProvider
{
    private const string Espanha = "España";

    public async Task<IReadOnlyList<CitySuggestion>> SearchCitiesAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
        {
            return [];
        }

        var normalizedQuery = query.Trim().ToUpperInvariant();
        var cacheKey = $"cities:v1:{normalizedQuery}";
        var cachedCities = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedCities))
        {
            return JsonSerializer.Deserialize<CitySuggestion[]>(cachedCities) ?? [];
        }

        try
        {
            using var searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            searchCancellation.CancelAfter(TimeSpan.FromSeconds(2));
            var response = await httpClient.GetFromJsonAsync<GeocodingResponse>($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=10&language=es&format=json", searchCancellation.Token);
            var results = response?.Results?.Select(result => new CitySuggestion(result.Name ?? string.Empty, result.Country, result.Admin1)).Where(result => !string.IsNullOrWhiteSpace(result.Name)).ToArray() ?? [];
            var cities = results.Length > 0 ? results : FindFallbackCities(query);
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cities), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }, cancellationToken);
            return cities;
        }
        catch (HttpRequestException exception)
        {
            LogNoResponse(logger, exception, query);
            return await CacheFallbackCitiesAsync(cacheKey, query, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await CacheFallbackCitiesAsync(cacheKey, query, cancellationToken);
        }
    }
    public async Task<WeatherForecast?> GetAsync(string destination, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destination) || endDate < startDate)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Open-Meteo exposes 16 calendar days counting today, hence today + 15.
        var lastForecastDate = today.AddDays(15);
        if (endDate < today || startDate > lastForecastDate)
        {
            return null;
        }

        var forecastStart = startDate < today ? today : startDate;
        var forecastEnd = endDate > lastForecastDate ? lastForecastDate : endDate;
        var cacheKey = $"weather:v3:{destination.Trim().ToUpperInvariant()}:{forecastStart:yyyyMMdd}:{forecastEnd:yyyyMMdd}";
        var cachedForecast = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedForecast))
        {
            return JsonSerializer.Deserialize<WeatherForecast>(cachedForecast);
        }

        try
        {
            var city = destination.Split(',', 2)[0].Trim();
            var location = await httpClient.GetFromJsonAsync<GeocodingResponse>($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1", cancellationToken);
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
    private sealed record GeocodingResult(decimal Latitude, decimal Longitude, string? Name = null, string? Country = null, string? Admin1 = null);
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

    private static CitySuggestion[] FindFallbackCities(string query)
    {
        var cities = new[]
        {
            new CitySuggestion("Toledo", Espanha, "Castilla-La Mancha"), new CitySuggestion("Tolosa", Espanha, "Gipuzkoa"), new CitySuggestion("Toluca", "México", "Estado de México"),
            new CitySuggestion("Madrid", Espanha, "Comunidad de Madrid"), new CitySuggestion("Madridejos", Espanha, "Castilla-La Mancha"), new CitySuggestion("Madras", "India", "Tamil Nadu"),
            new CitySuggestion("Washington, D.C.", "Estados Unidos", null), new CitySuggestion("Londres", "Reino Unido", "Inglaterra"), new CitySuggestion("Londrina", "Brasil", "Paraná"),
            new CitySuggestion("Barcelona", Espanha, "Cataluña"), new CitySuggestion("Valencia", Espanha, "Comunidad Valenciana"), new CitySuggestion("Roma", "Italia", "Lacio"), new CitySuggestion("París", "Francia", "Isla de Francia")
        };
        return cities.Where(city => city.DisplayName.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).Take(10).ToArray();
    }

    private async Task<CitySuggestion[]> CacheFallbackCitiesAsync(string cacheKey, string query, CancellationToken cancellationToken)
    {
        var cities = FindFallbackCities(query);
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cities), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }, cancellationToken);
        return cities;
    }
}
