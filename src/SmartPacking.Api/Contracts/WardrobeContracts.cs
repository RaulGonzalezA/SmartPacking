using SmartPacking.Domain;

namespace SmartPacking.Api.Contracts;

#pragma warning disable S6964 // Request DTOs deliberately use non-nullable primitives; controller validation enforces ranges.
public sealed record ClothingItemResponse(Guid Id, string Name, ClothingType Type, Season Season, string Color, int WarmthLevel, bool Waterproof, Style Style, int? WeightGrams, bool IsClean, bool IsAvailable, int PreferenceScore, IReadOnlyCollection<Guid> CombinesWith, bool IsDeleted, Guid? OwnerProfileId);
public sealed record UpsertClothingItemRequest(string Name, ClothingType Type, Season Season, string Color, int WarmthLevel, bool Waterproof, Style Style, int? WeightGrams, bool IsClean, bool IsAvailable, int PreferenceScore, IReadOnlyCollection<Guid>? CombinesWith, Guid? OwnerProfileId);
public sealed record UpdateClothingStatusRequest(bool IsClean, bool IsAvailable);

public static class WardrobeMappings
{
    public static ClothingItemResponse ToResponse(this ClothingItem item) => new(item.Id, item.Name, item.Type, item.Season, item.Color, item.WarmthLevel, item.Waterproof, item.Style, item.WeightGrams, item.IsClean, item.IsAvailable, item.PreferenceScore, item.CombinesWith, item.IsDeleted, item.OwnerProfileId);

    public static ClothingItem ToDomain(this UpsertClothingItemRequest request, Guid id, bool isDeleted = false) =>
        new(id, request.Name.Trim(), request.Type, request.Season, request.Color.Trim(), request.WarmthLevel, request.Waterproof, request.Style, request.WeightGrams, request.IsClean, request.IsAvailable, request.PreferenceScore, request.CombinesWith ?? [], isDeleted, request.OwnerProfileId);
}
#pragma warning restore S6964
