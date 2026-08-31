using SmartPacking.Domain;

namespace SmartPacking.Application;

public interface ISmartPackingStore
{
    Task SeedAsync(CancellationToken cancellationToken);
    Task<UserProfile> GetDefaultUserAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyProfile>> GetFamilyProfilesAsync(Guid userId, CancellationToken cancellationToken);
    Task<FamilyProfile> AddFamilyProfileAsync(Guid userId, FamilyProfile profile, CancellationToken cancellationToken);
    Task<FamilyProfile?> UpdateFamilyProfileAsync(Guid userId, FamilyProfile profile, CancellationToken cancellationToken);
    Task<bool> ArchiveFamilyProfileAsync(Guid userId, Guid profileId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyProfile>> GetTripProfilesAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task SetTripProfilesAsync(Guid userId, Guid tripId, IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClothingItem>> GetWardrobeAsync(Guid userId, CancellationToken cancellationToken);
    Task<ClothingItem> AddClothingItemAsync(Guid userId, ClothingItem item, CancellationToken cancellationToken);
    Task<bool> DeleteClothingItemAsync(Guid userId, Guid clothingItemId, CancellationToken cancellationToken);
    Task<bool> RestoreClothingItemAsync(Guid userId, Guid clothingItemId, CancellationToken cancellationToken);
    Task<ClothingItem?> UpdateClothingItemAsync(Guid userId, ClothingItem item, CancellationToken cancellationToken);
    Task<bool> UpdateClothingStatusAsync(Guid userId, Guid clothingItemId, bool isClean, bool isAvailable, CancellationToken cancellationToken);
    Task<IReadOnlyList<Trip>> GetTripsAsync(Guid userId, CancellationToken cancellationToken);
    Task<Trip> AddTripAsync(Guid userId, Trip trip, CancellationToken cancellationToken);
    Task<bool> DeleteTripAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task<Trip?> GetTripAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task<PackingList?> GetPackingListAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task<PackingList> SavePackingListAsync(PackingList packingList, CancellationToken cancellationToken);
    Task SetPackedAsync(Guid userId, Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken);
    Task<ProfilePackingList?> GetProfilePackingListAsync(Guid userId, Guid tripId, Guid profileId, CancellationToken cancellationToken);
    Task<ProfilePackingList> SaveProfilePackingListAsync(ProfilePackingList packingList, CancellationToken cancellationToken);
    Task SetProfilePackedAsync(Guid userId, Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChecklistItem>> GetChecklistAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChecklistItem>> AddChecklistItemsAsync(Guid userId, IReadOnlyCollection<ChecklistItem> items, CancellationToken cancellationToken);
    Task SetChecklistPackedAsync(Guid userId, Guid checklistItemId, bool isPacked, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClothingUsage>> GetUsageAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task SaveUsageAsync(Guid userId, Guid tripId, IReadOnlyCollection<ClothingUsage> usage, CancellationToken cancellationToken);
}
