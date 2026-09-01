using SmartPacking.Domain;

namespace SmartPacking.Application;

/// <summary>Operations required by the interactive web client.</summary>
public interface IWebSmartPackingClient
{
    Task<IReadOnlyList<Trip>> GetTripsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyProfile>> GetProfilesAsync(CancellationToken cancellationToken);
    Task<FamilyProfile> CreateProfileAsync(string name, string? packingNotes, string? medicalNotes, CancellationToken cancellationToken);
    Task UpdateProfileAsync(Guid profileId, string name, string? packingNotes, string? medicalNotes, CancellationToken cancellationToken);
    Task ArchiveProfileAsync(Guid profileId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyProfile>> GetTripProfilesAsync(Guid tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClothingItem>> GetWardrobeAsync(bool deleted, CancellationToken cancellationToken);
    Task<ProfileTripPackingPlan?> GetProfilePackingListAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripTemplate>> GetTripTemplatesAsync(CancellationToken cancellationToken);
    Task<TripWeatherForecast?> GetWeatherAsync(Guid tripId, CancellationToken cancellationToken);
    Task<LuggageRulesSummary?> GetLuggageRulesAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChecklistItem>> GetChecklistAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken);
    Task CreateTripAsync(string destination, DateOnly startDate, DateOnly endDate, int minimumTemperatureCelsius, int maximumTemperatureCelsius, string? templateKey, int luggageAllowanceGrams, bool cabinOnly, CancellationToken cancellationToken);
    Task UpdateTripAsync(Trip trip, CancellationToken cancellationToken);
    Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken);
    Task SetTripProfilesAsync(Guid tripId, IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken);
    Task CreateClothingAsync(ClothingItem item, CancellationToken cancellationToken);
    Task UpdateClothingStatusAsync(Guid clothingItemId, bool isClean, bool isAvailable, CancellationToken cancellationToken);
    Task DeleteClothingAsync(Guid clothingItemId, CancellationToken cancellationToken);
    Task RestoreClothingAsync(Guid clothingItemId, CancellationToken cancellationToken);
    Task<string> UploadClothingPhotoAsync(Guid clothingItemId, Stream content, string contentType, string fileName, CancellationToken cancellationToken);
    Task SetProfilePackedAsync(Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken);
    Task AddProfilePackingListItemAsync(Guid packingListId, Guid clothingItemId, CancellationToken cancellationToken);
    Task SetChecklistPackedAsync(Guid itemId, bool isPacked, CancellationToken cancellationToken);
}
