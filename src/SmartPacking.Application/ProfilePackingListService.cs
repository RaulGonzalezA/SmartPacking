using SmartPacking.Domain;

namespace SmartPacking.Application;

public sealed record ProfileTripPackingPlan(FamilyProfile Profile, TripPackingPlan Plan);

public sealed class ProfilePackingListService(ISmartPackingStore store)
{
    public async Task<ProfileTripPackingPlan?> GetOrCreateAsync(Guid userId, Guid tripId, Guid profileId, CancellationToken cancellationToken)
    {
        var profile = (await store.GetTripProfilesAsync(userId, tripId, cancellationToken)).SingleOrDefault(item => item.Id == profileId);
        var trip = await store.GetTripAsync(userId, tripId, cancellationToken);
        if (profile is null || trip is null) return null;

        var wardrobe = (await store.GetWardrobeAsync(userId, cancellationToken)).Where(item => item.OwnerProfileId is null || item.OwnerProfileId == profileId).ToArray();
        var recommendation = PackingRecommendationService.Recommend(trip, wardrobe);
        var packingList = await store.GetProfilePackingListAsync(userId, tripId, profileId, cancellationToken)
            ?? await store.SaveProfilePackingListAsync(new ProfilePackingList(Guid.NewGuid(), tripId, profileId, userId, DateTimeOffset.UtcNow,
                recommendation.Items.Select(item => new PackingListItem(item.Item.Id, false)).ToArray()), cancellationToken);
        var wardrobeByItem = wardrobe.ToDictionary(item => item.Id);
        var recommendationByItem = recommendation.Items.ToDictionary(item => item.Item.Id);
        var items = packingList.Items.Where(item => wardrobeByItem.ContainsKey(item.ClothingItemId)).Select(item => new PlannedItem(
            recommendationByItem.GetValueOrDefault(item.ClothingItemId)
                ?? new RecommendedItem(wardrobeByItem[item.ClothingItemId], 0, ["retirada del armario; se conserva por ser una maleta existente"]), item.IsPacked)).ToArray();
        var plan = new TripPackingPlan(trip, packingList.Id, items, items.Sum(item => item.Recommendation.Item.WeightGrams ?? 0));
        return new ProfileTripPackingPlan(profile, plan);
    }
}
