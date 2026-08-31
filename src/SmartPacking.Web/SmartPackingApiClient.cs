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

    public async Task CreateTripAsync(string destination, DateOnly startDate, DateOnly endDate, int minimumTemperatureCelsius, int maximumTemperatureCelsius, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/trips", new { destination, startDate, endDate, minimumTemperatureCelsius, maximumTemperatureCelsius, activities = new[] { Style.Casual } }, cancellationToken);
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
            item.Name, item.Type, item.Season, item.Color, item.WarmthLevel, item.Waterproof, item.Style, item.WeightGrams,
            item.IsClean, item.IsAvailable, item.PreferenceScore, item.CombinesWith, item.OwnerProfileId
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
}
