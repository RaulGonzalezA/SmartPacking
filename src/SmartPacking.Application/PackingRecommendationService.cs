using SmartPacking.Domain;

namespace SmartPacking.Application;

public sealed record RecommendedItem(ClothingItem Item, decimal Score, IReadOnlyList<string> Reasons);
public sealed record PackingRecommendation(Trip Trip, IReadOnlyList<RecommendedItem> Items, int TotalWeightGrams);

public static class PackingRecommendationService
{
    public static PackingRecommendation Recommend(Trip trip, IEnumerable<ClothingItem> wardrobe)
    {
        var availableItems = wardrobe.Where(item => !item.IsDeleted && item.IsClean && item.IsAvailable).ToArray();
        var selected = new List<RecommendedItem>();

        foreach (var type in RequiredTypes(trip, availableItems))
        {
            var count = NumberToPack(type, trip);
            selected.AddRange(availableItems
                .Where(item => item.Type == type)
                .Select(item => Score(item, trip, availableItems))
                .OrderByDescending(item => item.Score)
                .Take(count));
        }

        var uniqueItems = selected
            .GroupBy(item => item.Item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.Score)
            .ToArray();

        return new PackingRecommendation(trip, uniqueItems, uniqueItems.Sum(item => item.Item.WeightGrams ?? 0));
    }

    private static IEnumerable<ClothingType> RequiredTypes(Trip trip, IReadOnlyCollection<ClothingItem> wardrobe)
    {
        yield return ClothingType.TShirt;
        yield return ClothingType.Trousers;
        if (trip.MaximumTemperatureCelsius >= 24 && wardrobe.Any(item => item.Type == ClothingType.Shorts))
        {
            yield return ClothingType.Shorts;
        }

        if (trip.MaximumTemperatureCelsius >= 18 && wardrobe.Any(item => item.Type == ClothingType.Sandals))
        {
            yield return ClothingType.Sandals;
        }

        yield return ClothingType.Shoes;
        if (trip.MinimumTemperatureCelsius < 18 && wardrobe.Any(item => item.Type == ClothingType.Jacket))
        {
            yield return ClothingType.Jacket;
        }
    }

    private static int NumberToPack(ClothingType type, Trip trip) => type switch
    {
        ClothingType.TShirt => Math.Min(4, Math.Max(2, (int)Math.Ceiling(trip.Days / 2m))),
        ClothingType.Trousers or ClothingType.Shorts => Math.Min(2, Math.Max(1, (int)Math.Ceiling(trip.Days / 3m))),
        _ => 1
    };

    private static RecommendedItem Score(ClothingItem item, Trip trip, IReadOnlyCollection<ClothingItem> wardrobe)
    {
        var weather = WeatherScore(item, trip);
        var activity = trip.Activities.Contains(item.Style) || item.Style == Style.Casual ? 100 : 45;
        var combination = wardrobe.Count(other => item.CombinesWith.Contains(other.Id)) * 20;
        var score = weather * .30m + 100m * .20m + Math.Min(combination, 100) * .20m + activity * .20m + item.PreferenceScore * .10m;
        var reasons = new List<string>();
        if (weather >= 80)
        {
            reasons.Add("adecuada para el tiempo previsto");
        }

        if (activity >= 100)
        {
            reasons.Add("encaja con las actividades del viaje");
        }

        if (combination > 0)
        {
            reasons.Add($"combina con {combination / 20} prendas de tu armario");
        }

        if (item.PreferenceScore >= 80)
        {
            reasons.Add("es una de tus prendas preferidas");
        }

        return new RecommendedItem(item, decimal.Round(score, 1), reasons);
    }

    private static decimal WeatherScore(ClothingItem item, Trip trip)
    {
        if (trip.MaximumTemperatureCelsius >= 28)
        {
            return item.Season is Season.Summer or Season.AllYear && item.WarmthLevel <= 3 ? 100 : 35;
        }

        if (trip.MinimumTemperatureCelsius <= 12)
        {
            return item.Season is Season.Winter or Season.AllYear && item.WarmthLevel >= 5 ? 100 : 35;
        }

        return item.Season is Season.MidSeason or Season.AllYear ? 100 : 70;
    }
}
