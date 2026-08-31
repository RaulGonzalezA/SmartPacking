using SmartPacking.Domain;

namespace SmartPacking.Application;

/// <summary>Operations required by the interactive web client.</summary>
public interface IWebSmartPackingClient
{
    Task<IReadOnlyList<Trip>> GetTripsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyProfile>> GetProfilesAsync(CancellationToken cancellationToken);
    Task<FamilyProfile> CreateProfileAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyProfile>> GetTripProfilesAsync(Guid tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClothingItem>> GetWardrobeAsync(bool deleted, CancellationToken cancellationToken);
    Task<ProfileTripPackingPlan?> GetProfilePackingListAsync(Guid tripId, Guid profileId, CancellationToken cancellationToken);
    Task CreateTripAsync(string destination, DateOnly startDate, DateOnly endDate, int minimumTemperatureCelsius, int maximumTemperatureCelsius, CancellationToken cancellationToken);
    Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken);
    Task SetTripProfilesAsync(Guid tripId, IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken);
    Task CreateClothingAsync(ClothingItem item, CancellationToken cancellationToken);
    Task UpdateClothingStatusAsync(Guid clothingItemId, bool isClean, bool isAvailable, CancellationToken cancellationToken);
    Task DeleteClothingAsync(Guid clothingItemId, CancellationToken cancellationToken);
    Task RestoreClothingAsync(Guid clothingItemId, CancellationToken cancellationToken);
    Task SetProfilePackedAsync(Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken);
}
