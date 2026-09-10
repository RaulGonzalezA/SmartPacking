using SmartPacking.Domain;

namespace SmartPacking.Application;

/// <summary>One paged snapshot of the active and retired items in a wardrobe.</summary>
public sealed record WardrobeSnapshot(
    IReadOnlyList<ClothingItem> Items,
    IReadOnlyList<ClothingItem> DeletedItems,
    int Page,
    int PageSize);
