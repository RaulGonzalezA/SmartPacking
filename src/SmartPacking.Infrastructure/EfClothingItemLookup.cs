using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Infrastructure;

public sealed class EfClothingItemLookup(SmartPackingDbContext dbContext) : IClothingItemLookup
{
    public async Task<ClothingItem?> GetAsync(Guid userId, Guid clothingItemId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ClothingItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.Id == clothingItemId,
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var combinesWith = JsonSerializer.Deserialize<Guid[]>(entity.CombinationIds) ?? [];
        return new ClothingItem(
            entity.Id,
            entity.Name,
            (ClothingType)entity.Type,
            (Season)entity.Season,
            entity.Color,
            entity.WarmthLevel,
            entity.Waterproof,
            (Style)entity.Style,
            entity.WeightGrams,
            entity.IsClean,
            entity.IsAvailable,
            entity.PreferenceScore,
            combinesWith,
            entity.IsDeleted,
            entity.OwnerProfileId,
            entity.PhotoUrl,
            entity.Material);
    }
}
