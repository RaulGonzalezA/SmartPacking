using SmartPacking.Domain;

namespace SmartPacking.Application;

public interface ISmartPackingStore
{
    Task SeedAsync(CancellationToken cancellationToken);
    Task<UserProfile> GetDefaultUserAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ClothingItem>> GetWardrobeAsync(Guid userId, CancellationToken cancellationToken);
    Task<ClothingItem> AddClothingItemAsync(Guid userId, ClothingItem item, CancellationToken cancellationToken);
    Task<bool> UpdateClothingStatusAsync(Guid userId, Guid clothingItemId, bool isClean, bool isAvailable, CancellationToken cancellationToken);
    Task<IReadOnlyList<Trip>> GetTripsAsync(Guid userId, CancellationToken cancellationToken);
    Task<Trip> AddTripAsync(Guid userId, Trip trip, CancellationToken cancellationToken);
    Task<Trip?> GetTripAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task<PackingList?> GetPackingListAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task<PackingList> SavePackingListAsync(PackingList packingList, CancellationToken cancellationToken);
    Task SetPackedAsync(Guid userId, Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken);
}
