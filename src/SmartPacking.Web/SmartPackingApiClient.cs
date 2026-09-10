using System.Net.Http.Json;
using SmartPacking.Application;
using SmartPacking.Contracts;
using SmartPacking.Domain;

namespace SmartPacking.Web;

public sealed class SmartPackingApiClient(HttpClient httpClient) : IWebSmartPackingClient
{
    public async Task<IReadOnlyList<CitySuggestion>> SearchCitiesAsync(string query, CancellationToken cancellationToken) => await httpClient.GetFromJsonAsync<CitySuggestion[]>($"api/cities?query={Uri.EscapeDataString(query)}", cancellationToken) ?? [];

    public async Task<UserProfile> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<UserProfile>("api/me", cancellationToken)
        ?? throw new InvalidOperationException("La API no devolvió el usuario actual.");

    public async Task<GarmentRecognitionUsageResult> GetAiUsageAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<GarmentRecognitionUsageResult>("api/me/ai-usage", cancellationToken)
        ?? throw new InvalidOperationException("La API no devolvió el consumo de IA.");

    public async Task<IReadOnlyList<AdminUserSummary>> GetAdminUsersAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<AdminUserSummary[]>("api/admin/users", cancellationToken) ?? [];

    public async Task SetAdminPlanAsync(Guid userId, string plan, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/admin/users/{userId}/plan", new { plan }, cancellationToken));

    public async Task AddAdminCreditsAsync(Guid userId, int credits, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PostAsJsonAsync($"api/admin/users/{userId}/credits", new { credits }, cancellationToken));

    public async Task<IReadOnlyList<AdminAuditEntry>> GetAdminAuditAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<AdminAuditEntry[]>("api/admin/audit", cancellationToken) ?? [];

    public async Task<UserProfile> CompleteOnboardingAsync(string name, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/me/onboarding", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>(cancellationToken)
            ?? throw new InvalidOperationException("La API no devolvió el perfil actualizado.");
    }

    public async Task<UserProfile> UpdateCurrentUserAsync(string name, UserAddress? address, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync("api/me", new { name, street = address?.Street, postalCode = address?.PostalCode, city = address?.City, region = address?.Region }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>(cancellationToken)
            ?? throw new InvalidOperationException("La API no devolvió el perfil actualizado.");
    }

    public async Task DeleteCurrentUserAsync(string confirmation, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "api/me")
        {
            Content = JsonContent.Create(new { confirmation })
        }, cancellationToken));

    public async Task<IReadOnlyList<Trip>> GetTripsAsync(CancellationToken cancellationToken) =>
        (await httpClient.GetFromJsonAsync<TripResponse[]>("api/trips", cancellationToken) ?? []).Select(ToTrip).ToArray();

    public async Task<IReadOnlyList<FamilyProfile>> GetProfilesAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<FamilyProfile[]>("api/profiles", cancellationToken) ?? [];

    public async Task<FamilyProfile> CreateProfileAsync(string name, string? packingNotes, string? medicalNotes, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/profiles", new { name, packingNotes, medicalNotes }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FamilyProfile>(cancellationToken) ?? throw new InvalidOperationException("La API no devolvió el viajero creado.");
    }

    public async Task UpdateProfileAsync(Guid profileId, string name, string? packingNotes, string? medicalNotes, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/profiles/{profileId}", new { name, packingNotes, medicalNotes }, cancellationToken));

    public async Task ArchiveProfileAsync(Guid profileId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.DeleteAsync($"api/profiles/{profileId}", cancellationToken));

    public async Task<IReadOnlyList<FamilyProfile>> GetTripProfilesAsync(Guid tripId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<FamilyProfile[]>($"api/trips/{tripId}/profiles", cancellationToken) ?? [];

    public Task<TripDashboard?> GetTripDashboardAsync(Guid tripId, Guid selectedProfileId, CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<TripDashboard>($"api/trips/{tripId}/dashboard?profileId={selectedProfileId}", cancellationToken);

    public async Task<WardrobeSnapshot> GetWardrobeCollectionAsync(CancellationToken cancellationToken)
    {
        var result = await httpClient.GetFromJsonAsync<WardrobeCollectionDto>("api/wardrobe?includeDeleted=true", cancellationToken);
        return new WardrobeSnapshot(
            result?.Items.Select(item => item.ToDomain()).ToArray() ?? [],
            result?.DeletedItems.Select(item => item.ToDomain()).ToArray() ?? [],
            result?.Page ?? 1,
            result?.PageSize ?? 100);
    }

    public async Task<ProfileTripPackingPlan?> GetProfilePackingListAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<ProfileTripPackingPlan>($"api/trips/{tripId}/profiles/{profileId}/packing-list", cancellationToken);

    public async Task<IReadOnlyList<TripTemplate>> GetTripTemplatesAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<TripTemplate[]>("api/trips/templates", cancellationToken) ?? [];

    public async Task<TripWeatherForecast?> GetWeatherAsync(Guid tripId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"api/trips/{tripId}/weather", cancellationToken);
        response.EnsureSuccessStatusCode();
        var weather = await response.Content.ReadFromJsonAsync<WeatherForecastDto>(cancellationToken);
        return weather is null
            ? null
            : new TripWeatherForecast(
                weather.Destination,
                weather.MinimumCelsius,
                weather.MaximumCelsius,
                weather.RainProbability,
                weather.StartDate,
                weather.EndDate,
                weather.Daily.Select(day => new DailyTripForecast(day.Date, day.MinimumCelsius, day.MaximumCelsius, day.RainProbability, day.WeatherCode, day.ApparentMinimumCelsius, day.ApparentMaximumCelsius, day.WindSpeedKilometresPerHour)).ToArray());
    }

    public async Task<LuggageRulesSummary?> GetLuggageRulesAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<LuggageRulesSummary>($"api/trips/{tripId}/profiles/{profileId}/luggage-rules", cancellationToken);

    public async Task<IReadOnlyList<ChecklistItem>> GetChecklistAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<ChecklistItem[]>($"api/trips/{tripId}/profiles/{profileId}/checklist", cancellationToken) ?? [];

    public async Task AddProfileChecklistItemAsync(Guid tripId, Guid profileId, ChecklistCategory category, string name, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PostAsJsonAsync($"api/trips/{tripId}/profiles/{profileId}/checklist", new { category, name }, cancellationToken));

    public async Task<Trip> CreateTripAsync(Trip trip, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/trips", ToRequest(trip), cancellationToken);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TripResponse>(cancellationToken);
        return created is null ? throw new InvalidOperationException("La API no devolvió el viaje creado.") : ToTrip(created);
    }

    public async Task UpdateTripAsync(Trip trip, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/trips/{trip.Id}", ToRequest(trip), cancellationToken));

    public async Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.DeleteAsync($"api/trips/{tripId}", cancellationToken));

    public async Task SetTripProfilesAsync(Guid tripId, IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/trips/{tripId}/profiles", new { profileIds }, cancellationToken));

    public async Task<ClothingItem> CreateClothingAsync(ClothingItem item, CancellationToken cancellationToken)
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
            item.OwnerProfileId,
            item.Material
        }, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ClothingItemDto>>(cancellationToken);
        return result?.Data.ToDomain() ?? throw new InvalidOperationException("La API no devolvió la prenda creada.");
    }

    public async Task<GarmentRecognitionSuggestion> RecognizeGarmentAsync(Stream content, string contentType, string fileName, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "photo", fileName);
        using var response = await httpClient.PostAsync("api/wardrobe/recognition", form, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GarmentRecognitionSuggestion>(cancellationToken)
            ?? throw new InvalidOperationException("La API no devolvió una propuesta de prenda.");
    }

    public async Task UpdateClothingStatusAsync(Guid clothingItemId, bool isClean, bool isAvailable, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/wardrobe/{clothingItemId}/status", new { isClean, isAvailable }, cancellationToken));

    public async Task DeleteClothingAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.DeleteAsync($"api/wardrobe/{clothingItemId}", cancellationToken));

    public async Task RestoreClothingAsync(Guid clothingItemId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PostAsync($"api/wardrobe/{clothingItemId}/restore", null, cancellationToken));

    public async Task<string> UploadClothingPhotoAsync(Guid clothingItemId, Stream content, string contentType, string fileName, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "photo", fileName);
        using var response = await httpClient.PostAsync($"api/wardrobe/{clothingItemId}/photo", form, cancellationToken);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<PhotoUploadResponse>>(cancellationToken);
        return result?.Data.ImageUrl ?? throw new InvalidOperationException("La API no devolvió la dirección de la foto.");
    }

    public async Task<PhotoDownload?> GetClothingPhotoAsync(Guid clothingItemId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"api/wardrobe/{clothingItemId}/photo", cancellationToken);
            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return new PhotoDownload(content, response.Content.Headers.ContentType?.MediaType ?? "image/jpeg");
        }
        catch (ApiProblemException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task SetProfilePackedAsync(Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/profile-packing-lists/{packingListId}/items/{clothingItemId}", new { isPacked }, cancellationToken));

    public async Task AddProfilePackingListItemAsync(Guid packingListId, Guid clothingItemId, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PostAsJsonAsync($"api/profile-packing-lists/{packingListId}/items", new { clothingItemId }, cancellationToken));

    public async Task ApplyProfileRecommendationChangeAsync(Guid packingListId, Guid clothingItemId, RecommendationChangeKind changeKind, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PostAsJsonAsync($"api/profile-packing-lists/{packingListId}/recommendation-changes/apply", new { clothingItemId, changeKind }, cancellationToken));

    public async Task IgnoreProfileRecommendationChangeAsync(Guid packingListId, Guid clothingItemId, RecommendationChangeKind changeKind, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PostAsJsonAsync($"api/profile-packing-lists/{packingListId}/recommendation-changes/ignore", new { clothingItemId, changeKind }, cancellationToken));

    public async Task SetChecklistPackedAsync(Guid itemId, bool isPacked, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/checklist/{itemId}", new { isPacked }, cancellationToken));

    public async Task<IReadOnlyList<ClothingUsage>> GetUsageAsync(Guid tripId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<ClothingUsage[]>($"api/trips/{tripId}/usage", cancellationToken) ?? [];

    public async Task SaveUsageAsync(Guid tripId, IReadOnlyCollection<ClothingUsage> usage, CancellationToken cancellationToken) =>
        await EnsureSuccessAsync(await httpClient.PutAsJsonAsync($"api/trips/{tripId}/usage", usage, cancellationToken));

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return Task.CompletedTask;
    }

    private sealed record ClothingItemDto(Guid Id, string Name, ClothingType Type, Season Season, string Color, int WarmthLevel, bool Waterproof, Style Style, int? WeightGrams, bool IsClean, bool IsAvailable, int PreferenceScore, IReadOnlyCollection<Guid> CombinesWith, bool IsDeleted, Guid? OwnerProfileId, string? PhotoUrl, string? Material = null)
    {
        public ClothingItem ToDomain() => new(Id, Name, Type, Season, Color, WarmthLevel, Waterproof, Style, WeightGrams, IsClean, IsAvailable, PreferenceScore, CombinesWith, IsDeleted, OwnerProfileId, PhotoUrl, Material);
    }

    private sealed record WardrobeCollectionDto(ClothingItemDto[] Items, ClothingItemDto[] DeletedItems, int Page, int PageSize);

    private static Trip ToTrip(TripResponse trip) => new(trip.Id, trip.Destination, trip.StartDate, trip.EndDate, trip.MinimumTemperatureCelsius, trip.MaximumTemperatureCelsius, trip.Activities.Select(activity => (Style)activity).ToArray(), trip.TemplateKey, trip.LuggageAllowanceGrams, trip.CabinOnly, (LuggageType)trip.LuggageType, trip.LuggageHeightCentimetres, trip.LuggageWidthCentimetres, trip.LuggageDepthCentimetres, trip.DayPlans?.Select(plan => new TripDayPlan(plan.Date, plan.Activities.Select(activity => (TripActivity)activity).ToArray())).ToArray(), trip.AirlineCode, trip.TransportTypes?.Select(type => (TransportType)type).ToArray(), trip.Luggages?.Select(luggage => new TripLuggage(luggage.Id, (LuggageType)luggage.Type, luggage.AllowanceGrams, luggage.HeightCentimetres, luggage.WidthCentimetres, luggage.DepthCentimetres, luggage.Name)).ToArray(), trip.Origin, trip.TransportPlan is null ? null : new TransportPlan(trip.TransportPlan.Summary, trip.TransportPlan.Legs.Select(leg => new TransportLeg((TransportType)leg.Type, leg.From, leg.To, leg.EstimatedMinutes, leg.Description)).ToArray()), trip.Latitude, trip.Longitude);
    private static SaveTripRequest ToRequest(Trip trip) => new(trip.Destination, trip.StartDate, trip.EndDate, trip.MinimumTemperatureCelsius, trip.MaximumTemperatureCelsius, trip.Activities.Select(activity => (int)activity).ToArray(), trip.TemplateKey, trip.LuggageAllowanceGrams, trip.CabinOnly, (int)trip.LuggageType, trip.LuggageHeightCentimetres, trip.LuggageWidthCentimetres, trip.LuggageDepthCentimetres, trip.DayPlansOrEmpty.Select(plan => new TripDayPlanContract(plan.Date, plan.Activities.Select(activity => (int)activity).ToArray())).ToArray(), trip.AirlineCode, trip.TransportTypesOrEmpty.Select(type => (int)type).ToArray(), trip.LuggagesOrDefault.Select(luggage => new TripLuggageContract(luggage.Id, (int)luggage.Type, luggage.AllowanceGrams, luggage.HeightCentimetres, luggage.WidthCentimetres, luggage.DepthCentimetres, luggage.Name)).ToArray(), trip.Origin, trip.Latitude, trip.Longitude);

    private sealed record WeatherForecastDto(string Destination, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<DailyWeatherForecastDto> Daily);
    private sealed record DailyWeatherForecastDto(DateOnly Date, decimal MinimumCelsius, decimal MaximumCelsius, int RainProbability, int WeatherCode, decimal? ApparentMinimumCelsius, decimal? ApparentMaximumCelsius, decimal? WindSpeedKilometresPerHour);
    private sealed record PhotoUploadResponse(string ImageUrl);
}
