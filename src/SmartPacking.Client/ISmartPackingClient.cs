using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Client;

/// <summary>API operations required by native SmartPacking clients.</summary>
public interface ISmartPackingClient
{
    Task<UserProfile> GetCurrentUserAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Trip>> GetTripsAsync(CancellationToken cancellationToken);
    Task<TripDashboard?> GetTripDashboardAsync(Guid tripId, Guid? selectedProfileId, CancellationToken cancellationToken);
    Task SetChecklistPackedAsync(Guid itemId, bool isPacked, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClothingItem>> GetWardrobeAsync(CancellationToken cancellationToken);
    Task<GarmentRecognitionSuggestion> RecognizeGarmentAsync(ReadOnlyMemory<byte> jpegPhoto, string fileName, CancellationToken cancellationToken);
    Task<ClothingItem> CreateClothingItemAsync(CreateClothingItemRequest request, CancellationToken cancellationToken);
    Task UploadClothingPhotoAsync(Guid clothingItemId, ReadOnlyMemory<byte> jpegPhoto, string fileName, CancellationToken cancellationToken);
    Task<byte[]?> GetClothingPhotoAsync(Guid clothingItemId, CancellationToken cancellationToken);
}

public sealed record CreateClothingItemRequest(
    string Name,
    ClothingType Type,
    Season Season,
    string Color,
    int WarmthLevel,
    bool Waterproof,
    Style Style,
    int? WeightGrams,
    bool IsClean = true,
    bool IsAvailable = true,
    int PreferenceScore = 50,
    Guid? OwnerProfileId = null,
    string? Material = null);
