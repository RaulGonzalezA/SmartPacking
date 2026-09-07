using SmartPacking.Domain;

namespace SmartPacking.Application;

public sealed record DailyOutfit(DateOnly Date, string Activity, IReadOnlyList<ClothingItem> Items, string Explanation);

public static class OutfitRecommendationService
{
    public static IReadOnlyList<DailyOutfit> Create(Trip trip, IReadOnlyList<PlannedItem> plannedItems)
    {
        var available = plannedItems.Select(item => item.Recommendation.Item).ToArray();
        return Enumerable.Range(0, trip.Days).Select(offset =>
        {
            var date = trip.StartDate.AddDays(offset);
            var activities = trip.DayPlansOrEmpty.SingleOrDefault(plan => plan.Date == date)?.Activities ?? [TripActivity.Sightseeing];
            var isBeach = activities.Contains(TripActivity.Beach);
            var isFormal = activities.Contains(TripActivity.FormalEvent) || activities.Contains(TripActivity.Business);
            var cold = trip.MinimumTemperatureCelsius <= 16;
            var types = new List<ClothingType> { ClothingType.TShirt, ClothingType.Trousers, ClothingType.Shoes };
            if (isBeach)
            {
                types.Add(ClothingType.Swimwear);
            }

            if (cold)
            {
                types.Add(ClothingType.Sweater);
            }

            if (trip.MinimumTemperatureCelsius <= 10)
            {
                types.Add(ClothingType.Coat);
            }
            else if (cold)
            {
                types.Add(ClothingType.Jacket);
            }

            var items = types.Select(type => SelectItem(available, type, isFormal)).Where(item => item is not null).Cast<ClothingItem>().DistinctBy(item => item.Id).ToArray();
            var activity = string.Join(" · ", activities);
            return new DailyOutfit(date, activity, items, Explanation(isBeach, cold));
        }).ToArray();
    }

    private static ClothingItem? SelectItem(IEnumerable<ClothingItem> items, ClothingType type, bool isFormal) =>
        isFormal
            ? items.FirstOrDefault(item => item.Type == type && item.Style == Style.Formal) ?? items.FirstOrDefault(item => item.Type == type)
            : items.FirstOrDefault(item => item.Type == type);

    private static string Explanation(bool isBeach, bool cold)
    {
        if (isBeach)
        {
            return "Look preparado para playa o agua.";
        }

        return cold ? "Look con capas para el fresco previsto." : "Look ligero para las actividades previstas.";
    }
}
