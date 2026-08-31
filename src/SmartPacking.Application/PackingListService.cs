using SmartPacking.Domain;

namespace SmartPacking.Application;

public sealed record PlannedItem(RecommendedItem Recommendation, bool IsPacked);
public sealed record TripPackingPlan(Trip Trip, Guid PackingListId, IReadOnlyList<PlannedItem> Items, int TotalWeightGrams);

public sealed class PackingListService(ISmartPackingStore store, PackingRecommendationService recommendationService)
{
    public async Task<TripPackingPlan?> GetOrCreateAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await store.GetTripAsync(userId, tripId, cancellationToken);
        if (trip is null) return null;

        var wardrobe = await store.GetWardrobeAsync(userId, cancellationToken);
        var recommendation = recommendationService.Recommend(trip, wardrobe);
        var packingList = await store.GetPackingListAsync(userId, tripId, cancellationToken)
            ?? await store.SavePackingListAsync(
                new PackingList(Guid.NewGuid(), trip.Id, userId, DateTimeOffset.UtcNow,
                    recommendation.Items.Select(item => new PackingListItem(item.Item.Id, false)).ToArray()),
                cancellationToken);

        var packedByItem = packingList.Items.ToDictionary(item => item.ClothingItemId, item => item.IsPacked);
        var items = recommendation.Items
            .Select(item => new PlannedItem(item, packedByItem.GetValueOrDefault(item.Item.Id)))
            .ToArray();
        return new TripPackingPlan(trip, packingList.Id, items, recommendation.TotalWeightGrams);
    }
}
