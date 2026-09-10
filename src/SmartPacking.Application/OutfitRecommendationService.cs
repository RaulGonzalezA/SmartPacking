using SmartPacking.Domain;

namespace SmartPacking.Application;

public sealed record DailyOutfit(DateOnly Date, string Activity, IReadOnlyList<ClothingItem> Items, string Explanation);

public static class OutfitRecommendationService
{
    public static IReadOnlyList<DailyOutfit> Create(Trip trip, IReadOnlyList<PlannedItem> plannedItems, TripWeatherForecast? forecast = null)
    {
        var available = plannedItems.Select(item => item.Recommendation.Item).ToArray();
        var usage = new Dictionary<Guid, int>();
        return Enumerable.Range(0, trip.Days).Select(offset =>
        {
            var date = trip.StartDate.AddDays(offset);
            var activities = trip.DayPlansOrEmpty.SingleOrDefault(plan => plan.Date == date)?.Activities ?? [TripActivity.Sightseeing];
            var dailyForecast = forecast?.Daily.SingleOrDefault(day => day.Date == date);
            var isBeach = activities.Contains(TripActivity.Beach);
            var isBusiness = activities.Contains(TripActivity.Business);
            var isFormal = activities.Contains(TripActivity.FormalEvent);
            var minimum = dailyForecast?.ApparentMinimumCelsius ?? dailyForecast?.MinimumCelsius ?? trip.MinimumTemperatureCelsius;
            var maximum = dailyForecast?.ApparentMaximumCelsius ?? dailyForecast?.MaximumCelsius ?? trip.MaximumTemperatureCelsius;
            var cold = minimum <= 16;
            var rainExpected = dailyForecast?.RainProbability >= 45;
            var types = new List<ClothingType> { ClothingType.TShirt, ClothingType.Trousers, ClothingType.Shoes };
            if (isBeach)
            {
                types.Add(ClothingType.Swimwear);
            }

            if (cold)
            {
                types.Add(ClothingType.Sweater);
            }

            if (minimum <= 10)
            {
                types.Add(ClothingType.Coat);
            }
            else if (cold || rainExpected)
            {
                types.Add(ClothingType.Jacket);
            }

            var selected = new List<ClothingItem>();
            foreach (var type in types)
            {
                var item = SelectItem(available, type, isFormal, isBusiness, selected, usage);
                if (item is not null && selected.All(candidate => candidate.Id != item.Id))
                {
                    selected.Add(item);
                    usage[item.Id] = usage.GetValueOrDefault(item.Id) + 1;
                }
            }

            var activity = string.Join(" · ", activities);
            return new DailyOutfit(date, activity, selected, Explanation(isBeach, cold, rainExpected, minimum, maximum));
        }).ToArray();
    }

    private static ClothingItem? SelectItem(
        IEnumerable<ClothingItem> items,
        ClothingType type,
        bool isFormal,
        bool isBusiness,
        IReadOnlyCollection<ClothingItem> selected,
        IReadOnlyDictionary<Guid, int> usage) => items
            .Where(item => item.Type == type && selected.All(candidate => candidate.Id != item.Id))
            .OrderBy(item => DressCodePenalty(item, isFormal, isBusiness))
            .ThenBy(item => ReusePenalty(item, usage))
            .ThenByDescending(item => item.CombinesWith.Count(candidate => selected.Any(selectedItem => selectedItem.Id == candidate)))
            .ThenByDescending(item => item.PreferenceScore)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .FirstOrDefault();

    private static int DressCodePenalty(ClothingItem item, bool isFormal, bool isBusiness)
    {
        if (isBusiness)
        {
            return item.Style switch { Style.Business => 0, Style.Formal => 1, _ => 2 };
        }

        if (!isFormal)
        {
            return 0;
        }

        return item.Style is Style.Formal or Style.Business ? 0 : 1;
    }

    private static int ReusePenalty(ClothingItem item, IReadOnlyDictionary<Guid, int> usage)
    {
        var uses = usage.GetValueOrDefault(item.Id);
        var isReusable = item.Type is ClothingType.Shoes or ClothingType.Sandals or ClothingType.Jacket or ClothingType.Coat;
        return isReusable ? uses : uses * 10;
    }

    private static string Explanation(bool isBeach, bool cold, bool rainExpected, decimal minimum, decimal maximum)
    {
        if (isBeach)
        {
            return $"Look preparado para playa o agua · {minimum:0.#}–{maximum:0.#}° previstos.";
        }

        if (rainExpected)
        {
            return $"Incluye una capa para lluvia · {minimum:0.#}–{maximum:0.#}° previstos.";
        }

        return cold
            ? $"Look con capas para el fresco previsto · {minimum:0.#}–{maximum:0.#}°."
            : $"Look ligero para las actividades previstas · {minimum:0.#}–{maximum:0.#}°.";
    }
}
