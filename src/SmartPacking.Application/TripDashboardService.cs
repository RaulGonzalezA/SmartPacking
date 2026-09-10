using SmartPacking.Domain;

namespace SmartPacking.Application;

/// <summary>Builds the complete trip read model while sharing the trip, wardrobe and forecast queries between travellers.</summary>
public sealed class TripDashboardService(ISmartPackingStore store, IWeatherProvider weather)
{
    public async Task<TripDashboard?> GetAsync(Guid userId, Guid tripId, Guid? requestedProfileId, CancellationToken cancellationToken)
    {
        var trip = await store.GetTripAsync(userId, tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var forecast = await GetForecastAsync(trip, cancellationToken);
        var profiles = await store.GetTripProfilesAsync(userId, tripId, cancellationToken);
        var wardrobe = await store.GetWardrobeAsync(userId, cancellationToken);
        var savedLists = (await store.GetProfilePackingListsAsync(userId, tripId, cancellationToken)).ToDictionary(list => list.ProfileId);
        var checklists = (await store.GetProfileChecklistsAsync(userId, tripId, cancellationToken))
            .Where(item => item.ProfileId is not null)
            .GroupBy(item => item.ProfileId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ChecklistItem>)group.ToArray());
        var usage = await store.GetUsageAsync(userId, tripId, cancellationToken);
        var selectedProfileId = Guid.Empty;
        if (profiles.Any(profile => profile.Id == requestedProfileId))
        {
            selectedProfileId = requestedProfileId!.Value;
        }
        else if (profiles.Count > 0)
        {
            selectedProfileId = profiles[0].Id;
        }
        var familyPlans = new List<ProfileTripPackingPlan>(profiles.Count);
        var progress = new List<PreparationProgressItem>(profiles.Count);

        foreach (var profile in profiles)
        {
            var profileWardrobe = wardrobe.Where(item => item.OwnerProfileId is null || item.OwnerProfileId == profile.Id).ToArray();
            var recommendation = PackingRecommendationService.Recommend(trip, profileWardrobe, forecast.Forecast);
            if (!savedLists.TryGetValue(profile.Id, out var packingList))
            {
                packingList = await store.SaveProfilePackingListAsync(new ProfilePackingList(
                    Guid.NewGuid(), tripId, profile.Id, userId, DateTimeOffset.UtcNow,
                    recommendation.Items.Select(item => new PackingListItem(item.Item.Id, false)).ToArray()), cancellationToken);
                savedLists[profile.Id] = packingList;
            }

            if (!checklists.TryGetValue(profile.Id, out var checklist) || checklist.Count == 0)
            {
                checklist = await store.AddChecklistItemsAsync(userId, PackingChecklistDefaults.Create(tripId, profile.Id), cancellationToken);
                checklists[profile.Id] = checklist;
            }

            var plan = ProfilePackingListService.BuildPlan(trip, packingList, profileWardrobe, recommendation, forecast.Forecast);
            familyPlans.Add(new ProfileTripPackingPlan(profile, plan));
            progress.Add(new PreparationProgressItem(profile.Name, plan.Items.Count(item => item.IsPacked), plan.Items.Count, checklist.Count(item => item.IsPacked), checklist.Count));
        }

        var selectedPlan = familyPlans.SingleOrDefault(plan => plan.Profile.Id == selectedProfileId);
        var selectedChecklist = checklists.GetValueOrDefault(selectedProfileId) ?? [];
        var rules = selectedPlan is null ? null : BuildRules(trip, selectedPlan);
        var diff = selectedPlan is null
            ? null
            : CreateDiff(selectedPlan, savedLists.GetValueOrDefault(selectedProfileId), wardrobe, forecast.Forecast);
        return new TripDashboard(profiles, selectedProfileId, selectedPlan, familyPlans, selectedChecklist, progress, rules, usage, forecast.Forecast, forecast.Feedback, diff);
    }

    private async Task<(TripWeatherForecast? Forecast, string? Feedback)> GetForecastAsync(Trip trip, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (trip.EndDate < today)
        {
            return (null, "La previsión no está disponible para viajes ya finalizados.");
        }

        if (trip.StartDate > today.AddDays(15))
        {
            return (null, $"La previsión detallada estará disponible a partir del {trip.StartDate.AddDays(-15):d}.");
        }

        var forecast = await weather.GetAsync(trip.Destination, trip.StartDate, trip.EndDate, trip.Latitude, trip.Longitude, cancellationToken);
        return forecast is null
            ? (null, "No se ha podido obtener la previsión para este destino en este momento.")
            : (new TripWeatherForecast(forecast.Destination, forecast.MinimumCelsius, forecast.MaximumCelsius, forecast.RainProbability, forecast.StartDate, forecast.EndDate, forecast.Daily.Select(day => new DailyTripForecast(day.Date, day.MinimumCelsius, day.MaximumCelsius, day.RainProbability, day.WeatherCode, day.ApparentMinimumCelsius, day.ApparentMaximumCelsius, day.WindSpeedKilometresPerHour)).ToArray()), null);
    }

    private static PackingRecommendationDiff CreateDiff(ProfileTripPackingPlan plan, ProfilePackingList? list, IReadOnlyCollection<ClothingItem> wardrobe, TripWeatherForecast? forecast)
    {
        var recommendation = PackingRecommendationService.Recommend(plan.Plan.Trip, wardrobe.Where(item => item.OwnerProfileId is null || item.OwnerProfileId == plan.Profile.Id).ToArray(), forecast);
        var items = list?.Items ?? [];
        var ignored = items.Where(item => item.RecommendationDecision == RecommendationDecision.Ignored).Select(item => item.ClothingItemId).ToHashSet();
        return PackingRecommendationDiffService.Create(items, recommendation, wardrobe.ToDictionary(item => item.Id), ignored);
    }

    private static LuggageRulesSummary BuildRules(Trip trip, ProfileTripPackingPlan plan)
    {
        var weight = plan.Plan.TotalWeightGrams;
        var remaining = trip.LuggageAllowanceGrams - weight;
        var volume = plan.Plan.Items.Sum(item => EstimatedVolumeMillilitres(item.Recommendation.Item.Type));
        var capacity = trip.LuggageHeightCentimetres * trip.LuggageWidthCentimetres * trip.LuggageDepthCentimetres * 1000;
        return new LuggageRulesSummary(trip.LuggageAllowanceGrams, weight, remaining, trip.CabinOnly, remaining >= 0, 100, 1000, volume, capacity);
    }

    private static int EstimatedVolumeMillilitres(ClothingType type) => type switch
    {
        ClothingType.Jacket => 7000,
        ClothingType.Shoes => 6000,
        ClothingType.Trousers => 2500,
        ClothingType.Shorts => 1200,
        ClothingType.TShirt => 900,
        ClothingType.Sandals => 2500,
        _ => 500
    };
}
