using System.Net.Http.Json;
using SmartPacking.Application;
using SmartPacking.Contracts;
using SmartPacking.Domain;

namespace SmartPacking.Web;

public sealed class SmartPackingApiClient(HttpClient httpClient) : IWebSmartPackingClient
{
    public async Task<IReadOnlyList<Trip>> GetTripsAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<Trip[]>("api/trips", cancellationToken) ?? [];

    public async Task<IReadOnlyList<FamilyProfile>> GetProfilesAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<FamilyProfile[]>("api/profiles", cancellationToken) ?? [];

    public async Task<FamilyProfile> CreateProfileAsync(string name, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/profiles", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FamilyProfile>(cancellationToken) ?? throw new InvalidOperationException("La API no devolvió el viajero creado.");
    }

    public async Task UpdateProfileAsync(Guid profileId, string name, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/profiles/{profileId}", new { name }, cancellationToken));

    public async Task ArchiveProfileAsync(Guid profileId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.DeleteAsync($"api/profiles/{profileId}", cancellationToken));

    public async Task<IReadOnlyList<FamilyProfile>> GetTripProfilesAsync(Guid tripId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<FamilyProfile[]>($"api/trips/{tripId}/profiles", cancellationToken) ?? [];

    public async Task<IReadOnlyList<ClothingItem>> GetWardrobeAsync(bool deleted, CancellationToken cancellationToken)
    {
        var path = deleted ? "api/wardrobe/deleted" : "api/wardrobe";
        var result = await httpClient.GetFromJsonAsync<ApiResult<ClothingItemDto[]>>(path, cancellationToken);
        return result?.Data.Select(item => item.ToDomain()).ToArray() ?? [];
    }

    public async Task<ProfileTripPackingPlan?> GetProfilePackingListAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<ProfileTripPackingPlan>($"api/trips/{tripId}/profiles/{profileId}/packing-list", cancellationToken);

    public async Task<IReadOnlyList<TripTemplate>> GetTripTemplatesAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<TripTemplate[]>("api/trips/templates", cancellationToken) ?? [];

    public async Task<TripWeatherForecast?> GetWeatherAsync(Guid tripId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"api/trips/{tripId}/weather", cancellationToken);
            response.EnsureSuccessStatusCode();
            var weather = await response.Content.ReadFromJsonAsync<WeatherForecastDto>(cancellationToken);
            return weather is null ? null : new TripWeatherForecast(weather.Destination, weather.MinimumCelsius, weather.MaximumCelsius, weather.RainProbability, weather.StartDate, weather.EndDate, weather.Daily.Select(day => new DailyTripForecast(day.Date, day.MinimumCelsius, day.MaximumCelsius, day.RainProbability)).ToArray());
        }
        catch (ApiProblemException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task<LuggageRulesSummary?> GetLuggageRulesAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<LuggageRulesSummary>($"api/trips/{tripId}/profiles/{profileId}/luggage-rules", cancellationToken);

    public async Task CreateTripAsync(string destination, DateOnly startDate, DateOnly endDate, int minimumTemperatureCelsius, int maximumTemperatureCelsius, string? templateKey, int luggageAllowanceGrams, bool cabinOnly, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/trips", new { destination, startDate, endDate, minimumTemperatureCelsius, maximumTemperatureCelsius, activities = new[] { Style.Casual }, templateKey, luggageAllowanceGrams, cabinOnly }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.DeleteAsync($"api/trips/{tripId}", cancellationToken));

    public async Task SetTripProfilesAsync(Guid tripId, IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/trips/{tripId}/profiles", new { profileIds }, cancellationToken));

    public async Task CreateClothingAsync(ClothingItem item, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/wardrobe", new
        {
            item.Name,
            item.Type,
            item.Season,
            item.Color,
            item.WarmthLevel,
            item.Waterproof,
            item.Style,
            item.WeightGrams,
            item.IsClean,
            item.IsAvailable,
            item.PreferenceScore,
            item.CombinesWith,
            item.OwnerProfileId
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateClothingStatusAsync(Guid clothingItemId, bool isClean, bool isAvailable, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/wardrobe/{clothingItemId}/status", new { isClean, isAvailable }, cancellationToken));

    public async Task DeleteClothingAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.DeleteAsync($"api/wardrobe/{clothingItemId}", cancellationToken));

    public async Task RestoreClothingAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PostAsync($"api/wardrobe/{clothingItemId}/restore", null, cancellationToken));

    public async Task SetProfilePackedAsync(Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/profile-packing-lists/{packingListId}/items/{clothingItemId}", new { isPacked }, cancellationToken));

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return Task.CompletedTask;
    }

    private sealed record ClothingItemDto(Guid Id, string Name, ClothingType Type, Season Season, string Color, int WarmthLevel, bool Waterproof, Style Style, int? WeightGrams, bool IsClean, bool IsAvailable, int PreferenceScore, IReadOnlyCollection<Guid> CombinesWith, bool IsDeleted, Guid? OwnerProfileId)
    {
        public ClothingItem ToDomain() => new(Id, Name, Type, Season, Color, WarmthLevel, Waterproof, Style, WeightGrams, IsClean, IsAvailable, PreferenceScore, CombinesWith, IsDeleted, OwnerProfileId);
    }

    private sealed record WeatherForecastDto(string Destination, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<DailyWeatherForecastDto> Daily);
    private sealed record DailyWeatherForecastDto(DateOnly Date, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability);
}
