using SmartPacking.Domain;

namespace SmartPacking.Application;

public enum RecommendationChangeKind { Add, Remove }
public sealed record PackingRecommendationChange(RecommendationChangeKind Kind, ClothingItem Item, string Reason);
public sealed record PackingRecommendationDiff(IReadOnlyList<PackingRecommendationChange> Changes)
{
    public bool HasChanges => Changes.Count > 0;
}

public sealed record ResolvePackingRecommendationChangeCommand(Guid ClothingItemId, RecommendationChangeKind ChangeKind, bool Apply);

/// <summary>Compares a fresh recommendation with a persisted packing list without mutating user choices.</summary>
public static class PackingRecommendationDiffService
{
    public static PackingRecommendationDiff Create(
        IReadOnlyCollection<PackingListItem> currentItems,
        PackingRecommendation recommendation,
        IReadOnlyDictionary<Guid, ClothingItem> wardrobe,
        IReadOnlySet<Guid>? ignoredItemIds = null)
    {
        var ignored = ignoredItemIds ?? new HashSet<Guid>();
        var current = currentItems.Select(item => item.ClothingItemId).ToHashSet();
        var recommended = recommendation.Items.Select(item => item.Item.Id).ToHashSet();
        var changes = new List<PackingRecommendationChange>();

        foreach (var item in recommendation.Items.Where(item => !current.Contains(item.Item.Id) && !ignored.Contains(item.Item.Id)))
        {
            changes.Add(new(RecommendationChangeKind.Add, item.Item, string.Join(" · ", item.Reasons)));
        }

        foreach (var item in currentItems.Where(item => !item.IsManual && item.RecommendationDecision != RecommendationDecision.Ignored && !recommended.Contains(item.ClothingItemId) && !ignored.Contains(item.ClothingItemId)))
        {
            if (wardrobe.TryGetValue(item.ClothingItemId, out var clothingItem))
            {
                changes.Add(new(RecommendationChangeKind.Remove, clothingItem, "Ya no es necesaria según la previsión y el plan actual."));
            }
        }

        return new(changes);
    }
}
