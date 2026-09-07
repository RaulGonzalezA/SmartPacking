using SmartPacking.Domain;

namespace SmartPacking.Application;

public sealed record RecommendedItem(ClothingItem Item, decimal Score, IReadOnlyList<string> Reasons);
public sealed record PackingMissingItem(ClothingType Type, string Description);
public sealed record PackingRecommendation(Trip Trip, IReadOnlyList<RecommendedItem> Items, int TotalWeightGrams, IReadOnlyList<PackingMissingItem>? MissingItems = null);

public static class PackingRecommendationService
{
    public static PackingRecommendation Recommend(Trip trip, IEnumerable<ClothingItem> wardrobe, TripWeatherForecast? forecast = null)
    {
        var availableItems = wardrobe.Where(item => !item.IsDeleted && item.IsClean && item.IsAvailable).ToArray();
        var selected = new List<RecommendedItem>();

        foreach (var type in RequiredTypes(trip, availableItems, forecast))
        {
            var count = NumberToPack(type, trip);
            selected.AddRange(availableItems
                .Where(item => item.Type == type)
                .Select(item => Score(item, trip, availableItems, forecast))
                .OrderByDescending(item => item.Score)
                .Take(count));
        }

        var uniqueItems = selected
            .GroupBy(item => item.Item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.Score)
            .ToArray();

        var missing = RequiredTypes(trip, availableItems, forecast)
            .Where(type => availableItems.All(item => item.Type != type))
            .Distinct()
            .Select(type => new PackingMissingItem(type, MissingDescription(type, trip)))
            .ToArray();
        return new PackingRecommendation(trip, uniqueItems, uniqueItems.Sum(item => item.Item.WeightGrams ?? 0), missing);
    }

    private static IEnumerable<ClothingType> RequiredTypes(Trip trip, IReadOnlyCollection<ClothingItem> wardrobe, TripWeatherForecast? forecast)
    {
        var maximum = forecast?.MaximumCelsius ?? trip.MaximumTemperatureCelsius;
        var minimum = forecast?.MinimumCelsius ?? trip.MinimumTemperatureCelsius;
        yield return ClothingType.TShirt;
        yield return ClothingType.Trousers;
        yield return ClothingType.Underwear;
        yield return ClothingType.Socks;
        if (maximum >= 24 && wardrobe.Any(item => item.Type == ClothingType.Shorts))
        {
            yield return ClothingType.Shorts;
        }

        if (maximum >= 18 && wardrobe.Any(item => item.Type == ClothingType.Sandals))
        {
            yield return ClothingType.Sandals;
        }

        yield return ClothingType.Shoes;
        var dayActivities = trip.DayPlansOrEmpty.SelectMany(plan => plan.Activities).ToHashSet();
        if (maximum >= 24 && (trip.Activities.Contains(Style.Sport) || dayActivities.Contains(TripActivity.Beach) || dayActivities.Contains(TripActivity.Sport)))
        {
            yield return ClothingType.Swimwear;
        }

        if (minimum <= 16)
        {
            yield return ClothingType.Sweater;
        }

        if (minimum < 18)
        {
            yield return ClothingType.Jacket;
        }

        if (minimum <= 10)
        {
            yield return ClothingType.Coat;
        }
    }

    private static int NumberToPack(ClothingType type, Trip trip) => type switch
    {
        ClothingType.TShirt => Math.Min(4, Math.Max(2, (int)Math.Ceiling(trip.Days / 2m))),
        ClothingType.Trousers or ClothingType.Shorts => Math.Min(2, Math.Max(1, (int)Math.Ceiling(trip.Days / 3m))),
        ClothingType.Underwear or ClothingType.Socks => Math.Min(7, Math.Max(2, trip.Days)),
        _ => 1
    };

    private static string MissingDescription(ClothingType type, Trip trip) => type switch
    {
        ClothingType.Underwear => $"faltan prendas de ropa interior para {trip.Days} días",
        ClothingType.Socks => $"faltan calcetines para {trip.Days} días",
        ClothingType.Swimwear => "no tienes bañador para las actividades de playa o deporte",
        ClothingType.Sweater => "no tienes jersey para las temperaturas frescas previstas",
        ClothingType.Coat => "no tienes abrigo para las temperaturas frías previstas",
        ClothingType.Jacket => "no tienes una capa exterior para el tiempo previsto",
        _ => $"no tienes {type} disponible para este viaje",
    };

    private static RecommendedItem Score(ClothingItem item, Trip trip, IReadOnlyCollection<ClothingItem> wardrobe, TripWeatherForecast? forecast)
    {
        var weather = WeatherScore(item, trip, forecast);
        var activity = trip.Activities.Contains(item.Style) || item.Style == Style.Casual ? 100 : 45;
        var combination = wardrobe.Count(other => item.CombinesWith.Contains(other.Id)) * 20;
        var score = weather * .30m + 100m * .20m + Math.Min(combination, 100) * .20m + activity * .20m + item.PreferenceScore * .10m;
        var reasons = new List<string>();
        if (weather >= 80)
        {
            reasons.Add(forecast is null ? "adecuada para el tiempo previsto" : "adecuada para la previsión actual");
        }

        if (activity >= 100)
        {
            reasons.Add("encaja con las actividades del viaje");
        }

        if (item.Type is ClothingType.Underwear or ClothingType.Socks)
        {
            reasons.Add("prenda esencial diaria para la duración del viaje");
        }
        else if (item.Type == ClothingType.Swimwear)
        {
            reasons.Add("recomendada para actividad deportiva o de agua con calor");
        }
        else if (item.Type is ClothingType.Sweater or ClothingType.Coat)
        {
            reasons.Add("capa térmica recomendada por las temperaturas previstas");
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

    private static decimal WeatherScore(ClothingItem item, Trip trip, TripWeatherForecast? forecast)
    {
        var maximum = forecast?.MaximumCelsius ?? trip.MaximumTemperatureCelsius;
        var minimum = forecast?.MinimumCelsius ?? trip.MinimumTemperatureCelsius;
        if (maximum >= 28)
        {
            return item.Season is Season.Summer or Season.AllYear && item.WarmthLevel <= 3 ? 100 : 35;
        }

        if (minimum <= 12)
        {
            return item.Season is Season.Winter or Season.AllYear && item.WarmthLevel >= 5 ? 100 : 35;
        }

        return item.Season is Season.MidSeason or Season.AllYear ? 100 : 70;
    }
}
