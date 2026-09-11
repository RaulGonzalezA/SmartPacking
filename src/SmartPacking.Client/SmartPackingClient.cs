using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SmartPacking.Application;
using SmartPacking.Contracts;
using SmartPacking.Domain;

namespace SmartPacking.Client;

public sealed class SmartPackingClient(HttpClient httpClient) : ISmartPackingClient
{
    public async Task<UserProfile> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<UserProfile>("api/me", cancellationToken)
        ?? throw new InvalidOperationException("La API no devolvió el usuario actual.");

    public async Task<IReadOnlyList<Trip>> GetTripsAsync(CancellationToken cancellationToken) =>
        (await httpClient.GetFromJsonAsync<TripResponse[]>("api/trips", cancellationToken) ?? [])
            .Select(ToTrip)
            .ToArray();

    public Task<TripDashboard?> GetTripDashboardAsync(Guid tripId, Guid? selectedProfileId, CancellationToken cancellationToken)
    {
        var suffix = selectedProfileId.HasValue ? $"?profileId={selectedProfileId.Value}" : string.Empty;
        return httpClient.GetFromJsonAsync<TripDashboard>($"api/trips/{tripId}/dashboard{suffix}", cancellationToken);
    }

    public async Task SetChecklistPackedAsync(Guid itemId, bool isPacked, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/checklist/{itemId}", new { isPacked }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ClothingItem>> GetWardrobeAsync(CancellationToken cancellationToken) =>
        (await GetWardrobePageAsync(1, 100, cancellationToken)).Items;

    public async Task<WardrobePageResult> GetWardrobePageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var result = await httpClient.GetFromJsonAsync<ApiResult<ClothingItemDto[]>>(
            $"api/wardrobe?page={page}&pageSize={pageSize}",
            cancellationToken);
        var items = result?.Data.Select(ToClothingItem).ToArray() ?? [];
        return new WardrobePageResult(items, page, pageSize, items.Length == pageSize);
    }

    public async Task<GarmentRecognitionSuggestion> RecognizeGarmentAsync(byte[] jpegPhoto, string fileName, CancellationToken cancellationToken)
    {
        using var content = CreatePhotoContent(jpegPhoto, fileName, null);
        using var response = await httpClient.PostAsync("api/wardrobe/recognition", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GarmentRecognitionSuggestion>(cancellationToken)
            ?? throw new InvalidOperationException("La API no devolvió una propuesta de reconocimiento.");
    }

    public async Task<ClothingItem> CreateClothingItemAsync(CreateClothingItemRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/wardrobe", ToPayload(request), cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ClothingItemDto>>(cancellationToken)
            ?? throw new InvalidOperationException("La API no devolvió la prenda creada.");
        return ToClothingItem(result.Data);
    }

    public async Task<ClothingItem> UpdateClothingItemAsync(
        Guid clothingItemId,
        CreateClothingItemRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/wardrobe/{clothingItemId}", ToPayload(request), cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ClothingItemDto>>(cancellationToken)
            ?? throw new InvalidOperationException("La API no devolvió la prenda actualizada.");
        return ToClothingItem(result.Data);
    }

    public async Task DeleteClothingItemAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync($"api/wardrobe/{clothingItemId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadClothingPhotoAsync(
        Guid clothingItemId,
        byte[] jpegPhoto,
        string fileName,
        CancellationToken cancellationToken,
        byte[]? thumbnailJpeg = null)
    {
        using var content = CreatePhotoContent(jpegPhoto, fileName, thumbnailJpeg);
        using var response = await httpClient.PostAsync($"api/wardrobe/{clothingItemId}/photo", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<byte[]?> GetClothingPhotoAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        GetPhotoAsync($"api/wardrobe/{clothingItemId}/photo", cancellationToken);

    public Task<byte[]?> GetClothingThumbnailAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        GetPhotoAsync($"api/wardrobe/{clothingItemId}/thumbnail", cancellationToken);

    private async Task<byte[]?> GetPhotoAsync(string uri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static MultipartFormDataContent CreatePhotoContent(byte[] jpegPhoto, string fileName, byte[]? thumbnailJpeg)
    {
        var content = new MultipartFormDataContent();
        var photoContent = new ByteArrayContent(jpegPhoto);
        photoContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(photoContent, "photo", string.IsNullOrWhiteSpace(fileName) ? "garment.jpg" : fileName);

        if (thumbnailJpeg is not null)
        {
            var thumbnailContent = new ByteArrayContent(thumbnailJpeg);
            thumbnailContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(thumbnailContent, "thumbnail", "thumbnail.jpg");
        }

        return content;
    }

    private static UpsertClothingItemDto ToPayload(CreateClothingItemRequest request) => new(
        request.Name,
        request.Type,
        request.Season,
        request.Color,
        request.WarmthLevel,
        request.Waterproof,
        request.Style,
        request.WeightGrams,
        request.IsClean,
        request.IsAvailable,
        request.PreferenceScore,
        request.CombinesWith ?? [],
        request.OwnerProfileId,
        request.Material);

    private static ClothingItem ToClothingItem(ClothingItemDto item) => new(
        item.Id,
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
        item.IsDeleted,
        item.OwnerProfileId,
        item.PhotoUrl,
        item.Material);

    private static Trip ToTrip(TripResponse trip) => new(
        trip.Id,
        trip.Destination,
        trip.StartDate,
        trip.EndDate,
        trip.MinimumTemperatureCelsius,
        trip.MaximumTemperatureCelsius,
        trip.Activities.Select(activity => (Style)activity).ToArray(),
        trip.TemplateKey,
        trip.LuggageAllowanceGrams,
        trip.CabinOnly,
        (LuggageType)trip.LuggageType,
        trip.LuggageHeightCentimetres,
        trip.LuggageWidthCentimetres,
        trip.LuggageDepthCentimetres,
        trip.DayPlans?.Select(plan => new TripDayPlan(plan.Date, plan.Activities.Select(activity => (TripActivity)activity).ToArray())).ToArray(),
        trip.AirlineCode,
        trip.TransportTypes?.Select(type => (TransportType)type).ToArray(),
        trip.Luggages?.Select(luggage => new TripLuggage(luggage.Id, (LuggageType)luggage.Type, luggage.AllowanceGrams, luggage.HeightCentimetres, luggage.WidthCentimetres, luggage.DepthCentimetres, luggage.Name)).ToArray(),
        trip.Origin,
        trip.TransportPlan is null ? null : new TransportPlan(trip.TransportPlan.Summary, trip.TransportPlan.Legs.Select(leg => new TransportLeg((TransportType)leg.Type, leg.From, leg.To, leg.EstimatedMinutes, leg.Description)).ToArray()),
        trip.Latitude,
        trip.Longitude);

    private sealed record ClothingItemDto(
        Guid Id,
        string Name,
        ClothingType Type,
        Season Season,
        string Color,
        int WarmthLevel,
        bool Waterproof,
        Style Style,
        int? WeightGrams,
        bool IsClean,
        bool IsAvailable,
        int PreferenceScore,
        IReadOnlyCollection<Guid> CombinesWith,
        bool IsDeleted,
        Guid? OwnerProfileId,
        string? PhotoUrl,
        string? Material);

    private sealed record UpsertClothingItemDto(
        string Name,
        ClothingType Type,
        Season Season,
        string Color,
        int WarmthLevel,
        bool Waterproof,
        Style Style,
        int? WeightGrams,
        bool IsClean,
        bool IsAvailable,
        int PreferenceScore,
        IReadOnlyCollection<Guid> CombinesWith,
        Guid? OwnerProfileId,
        string? Material);
}
