using SmartPacking.Domain;

namespace SmartPacking.Application;

public sealed record WeightOptimizationSuggestion(string Action, ClothingItem Item, ClothingItem? Replacement, int SavedGrams);
public sealed record WeatherRecommendationChange(string Description, int TemperatureDifferenceCelsius);
public sealed record FamilyPackingSuggestion(string Description, int PotentialSavedGrams);
public sealed record LaundryReuseSuggestion(DateOnly LaundryDate, string Description, int EstimatedSavedGrams);
public sealed record PackingInsights(
    IReadOnlyList<WeightOptimizationSuggestion> WeightSuggestions,
    IReadOnlyList<WeatherRecommendationChange> WeatherChanges,
    IReadOnlyList<FamilyPackingSuggestion> SharedItems,
    IReadOnlyList<FamilyPackingSuggestion> Duplicates,
    IReadOnlyList<LaundryReuseSuggestion> LaundryReuse);

public static class PackingInsightsService
{
    public static PackingInsights Analyze(
        ProfileTripPackingPlan? selectedPlan,
        IReadOnlyList<ProfileTripPackingPlan> familyPlans,
        IReadOnlyList<ClothingItem> wardrobe,
        TripWeatherForecast? forecast)
    {
        if (selectedPlan is null)
        {
            return new([], [], [], [], []);
        }

        var plan = selectedPlan.Plan;
        var weightSuggestions = BuildWeightSuggestions(plan, wardrobe, selectedPlan.Profile.Id);
        var weatherChanges = BuildWeatherChanges(plan, wardrobe, selectedPlan.Profile.Id, forecast);
        var sharedItems = BuildSharedItems(familyPlans);
        var duplicates = BuildDuplicates(familyPlans);
        var laundryReuse = BuildLaundryReuse(plan);
        return new(weightSuggestions, weatherChanges, sharedItems, duplicates, laundryReuse);
    }

    private static List<WeightOptimizationSuggestion> BuildWeightSuggestions(TripPackingPlan plan, IReadOnlyList<ClothingItem> wardrobe, Guid profileId)
    {
        var excess = plan.TotalWeightGrams - plan.Trip.LuggageAllowanceGrams;
        if (excess <= 0)
        {
            return [];
        }

        var plannedIds = plan.Items.Select(item => item.Recommendation.Item.Id).ToHashSet();
        var candidates = wardrobe.Where(item => !item.IsDeleted && item.IsClean && item.IsAvailable &&
                                                (item.OwnerProfileId is null || item.OwnerProfileId == profileId) &&
                                                !plannedIds.Contains(item.Id)).ToArray();
        var suggestions = new List<WeightOptimizationSuggestion>();
        var saved = 0;

        foreach (var planned in plan.Items.OrderBy(item => item.Recommendation.Score).ThenByDescending(item => item.Recommendation.Item.WeightGrams ?? 0))
        {
            if (saved >= excess)
            {
                break;
            }

            var item = planned.Recommendation.Item;
            var replacement = candidates.Where(candidate => candidate.Type == item.Type && (candidate.WeightGrams ?? 0) < (item.WeightGrams ?? 0))
                .OrderByDescending(candidate => candidate.PreferenceScore)
                .ThenBy(candidate => candidate.WeightGrams ?? 0)
                .FirstOrDefault();
            var savedGrams = replacement is null
                ? item.WeightGrams ?? 0
                : (item.WeightGrams ?? 0) - (replacement.WeightGrams ?? 0);
            if (savedGrams <= 0)
            {
                continue;
            }

            suggestions.Add(new(replacement is null ? "Quita" : "Sustituye", item, replacement, savedGrams));
            saved += savedGrams;
        }

        return suggestions;
    }

    private static List<WeatherRecommendationChange> BuildWeatherChanges(TripPackingPlan plan, IReadOnlyList<ClothingItem> wardrobe, Guid profileId, TripWeatherForecast? forecast)
    {
        if (forecast is null)
        {
            return [];
        }

        var staticRecommendation = PackingRecommendationService.Recommend(plan.Trip, wardrobe.Where(item => item.OwnerProfileId is null || item.OwnerProfileId == profileId));
        var liveRecommendation = PackingRecommendationService.Recommend(plan.Trip, wardrobe.Where(item => item.OwnerProfileId is null || item.OwnerProfileId == profileId), forecast);
        var staticIds = staticRecommendation.Items.Select(item => item.Item.Id).ToHashSet();
        var liveIds = liveRecommendation.Items.Select(item => item.Item.Id).ToHashSet();
        var difference = (int)Math.Round(forecast.MaximumCelsius - plan.Trip.MaximumTemperatureCelsius);
        var changes = new List<WeatherRecommendationChange>();

        foreach (var added in liveRecommendation.Items.Where(item => !staticIds.Contains(item.Item.Id)).Take(2))
        {
            changes.Add(new($"Añade o prioriza {added.Item.Name}: la previsión actual la favorece.", difference));
        }

        foreach (var removed in staticRecommendation.Items.Where(item => !liveIds.Contains(item.Item.Id)).Take(2))
        {
            changes.Add(new($"{removed.Item.Name} deja de ser prioritaria con la previsión actual.", difference));
        }

        return changes;
    }

    private static FamilyPackingSuggestion[] BuildSharedItems(IReadOnlyList<ProfileTripPackingPlan> familyPlans) =>
        familyPlans.SelectMany(plan => plan.Plan.Items.Select(item => (plan.Profile.Name, Item: item.Recommendation.Item)))
            .Where(entry => entry.Item.OwnerProfileId is null)
            .GroupBy(entry => entry.Item.Id)
            .Where(group => group.Select(entry => entry.Name).Distinct().Count() > 1)
            .Select(group => new FamilyPackingSuggestion($"{group.First().Item.Name} puede ir como elemento compartido para {string.Join(", ", group.Select(entry => entry.Name).Distinct())}.", group.First().Item.WeightGrams ?? 0))
            .ToArray();

    private static FamilyPackingSuggestion[] BuildDuplicates(IReadOnlyList<ProfileTripPackingPlan> familyPlans) =>
        familyPlans.SelectMany(plan => plan.Plan.Items.Select(item => (plan.Profile.Name, Item: item.Recommendation.Item)))
            .Where(entry => entry.Item.OwnerProfileId is not null)
            .GroupBy(entry => (entry.Item.Type, Color: entry.Item.Color.Trim().ToUpperInvariant()))
            .Where(group => group.Select(entry => entry.Name).Distinct().Count() > 1)
            .Select(group =>
            {
                var items = group.OrderByDescending(entry => entry.Item.WeightGrams ?? 0).ToArray();
                return new FamilyPackingSuggestion($"Hay {items.Length} prendas similares ({items[0].Item.Type.ToString().ToLowerInvariant()} {items[0].Item.Color}) entre {string.Join(", ", items.Select(entry => entry.Name).Distinct())}; revisad si necesitáis todas.", items[0].Item.WeightGrams ?? 0);
            })
            .ToArray();

    private static LaundryReuseSuggestion[] BuildLaundryReuse(TripPackingPlan plan)
    {
        if (plan.Trip.Days < 6)
        {
            return [];
        }

        var laundryDate = plan.Trip.StartDate.AddDays(plan.Trip.Days / 2);
        var reusable = plan.Items.Where(item => item.Recommendation.Item.Type is ClothingType.TShirt or ClothingType.Trousers)
            .OrderByDescending(item => item.Recommendation.Item.PreferenceScore)
            .Take(2)
            .Select(item => item.Recommendation.Item)
            .ToArray();
        if (reusable.Length == 0)
        {
            return [];
        }

        var outfit = reusable.SelectMany(first => reusable.Where(second => second.Id != first.Id && first.CombinesWith.Contains(second.Id))
            .Select(second => new[] { first, second }))
            .FirstOrDefault();
        var reusedNames = outfit is null ? string.Join(" y ", reusable.Select(item => item.Name)) : string.Join(" + ", outfit.Select(item => item.Name));
        var saved = (outfit ?? reusable).Sum(item => item.WeightGrams ?? 0);
        var reuseLabel = outfit is null ? "reutiliza" : "repite el outfit";
        return [new(laundryDate, $"Planifica lavandería el {laundryDate:dd/MM} y {reuseLabel} {reusedNames}; podrás evitar llevar una segunda tanda equivalente.", saved)];
    }
}
