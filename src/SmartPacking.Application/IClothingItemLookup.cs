using SmartPacking.Domain;

namespace SmartPacking.Application;

public interface IClothingItemLookup
{
    Task<ClothingItem?> GetAsync(Guid userId, Guid clothingItemId, CancellationToken cancellationToken);
}
