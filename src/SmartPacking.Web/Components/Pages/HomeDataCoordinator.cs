using SmartPacking.Application;

namespace SmartPacking.Web.Components.Pages;

/// <summary>Coordinates isolated dashboard refreshes without coupling panels to API calls.</summary>
public sealed class HomeDataCoordinator(IWebSmartPackingClient api, ILogger<HomeDataCoordinator> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> LogPackingInsightsFailure = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(1, nameof(LogPackingInsightsFailure)),
        "No se pudieron calcular los insights para la maleta del viaje {TripId}");

    public async Task LoadInitialAsync(HomeViewModel state, CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            RefreshTripsAsync(state, cancellationToken),
            RefreshWardrobeAsync(state, cancellationToken));

        await RefreshTripDetailsAsync(state, cancellationToken);
    }

    public async Task RefreshTripsAsync(HomeViewModel state, CancellationToken cancellationToken)
    {
        state.TripsStatus.Begin();
        try
        {
            var trips = api.GetTripsAsync(cancellationToken);
            var profiles = api.GetProfilesAsync(cancellationToken);
            var templates = api.GetTripTemplatesAsync(cancellationToken);
            await Task.WhenAll(trips, profiles, templates);

            state.Trips = await trips;
            state.Profiles = await profiles;
            state.Templates = await templates;
            state.SelectInitialTrip();
            state.TripsStatus.Complete();
        }
        catch
        {
            state.TripsStatus.Fail("No se han podido cargar los viajes.");
            throw;
        }
    }

    public async Task RefreshWardrobeAsync(HomeViewModel state, CancellationToken cancellationToken)
    {
        state.WardrobeStatus.Begin();
        try
        {
            var wardrobe = api.GetWardrobeAsync(false, cancellationToken);
            var deletedWardrobe = api.GetWardrobeAsync(true, cancellationToken);
            await Task.WhenAll(wardrobe, deletedWardrobe);

            state.Wardrobe = await wardrobe;
            state.DeletedWardrobe = await deletedWardrobe;
            RefreshPackingInsights(state);
            state.WardrobeStatus.Complete();
        }
        catch
        {
            state.WardrobeStatus.Fail("No se ha podido cargar el armario.");
            throw;
        }
    }

    public async Task RefreshTripDetailsAsync(HomeViewModel state, CancellationToken cancellationToken)
    {
        if (state.SelectedTripId == Guid.Empty)
        {
            state.ClearSelectedTripData();
            state.PackingStatus.Complete();
            return;
        }

        state.PackingStatus.Begin();
        try
        {
            state.TripProfiles = await api.GetTripProfilesAsync(state.SelectedTripId, cancellationToken);
            state.EnsureSelectedProfile();
            await LoadPackingCoreAsync(state, cancellationToken);
            state.Weather = await api.GetWeatherAsync(state.SelectedTripId, cancellationToken);

            var usage = await api.GetUsageAsync(state.SelectedTripId, cancellationToken);
            state.UsageItemIds = usage.Count == 0
                ? state.Plan?.Plan.Items.Select(item => item.Recommendation.Item.Id).ToHashSet() ?? []
                : usage.Select(item => item.ClothingItemId).ToHashSet();
            state.UsedItemIds = usage.Where(item => item.WasUsed).Select(item => item.ClothingItemId).ToHashSet();

            var details = await Task.WhenAll(state.TripProfiles.Select(async profile =>
            {
                var plan = await api.GetProfilePackingListAsync(state.SelectedTripId, profile.Id, cancellationToken);
                var checklist = await api.GetChecklistAsync(state.SelectedTripId, profile.Id, cancellationToken);
                return (Plan: plan, Progress: new PreparationProgressItem(profile.Name, plan?.Plan.Items.Count(item => item.IsPacked) ?? 0, plan?.Plan.Items.Count ?? 0, checklist.Count(item => item.IsPacked), checklist.Count));
            }));
            state.FamilyPlans = details.Where(item => item.Plan is not null).Select(item => item.Plan!).ToArray();
            state.PreparationProgress = details.Select(item => item.Progress).ToArray();
            RefreshPackingInsights(state);
            state.PackingStatus.Complete();
        }
        catch
        {
            state.PackingStatus.Fail("No se ha podido actualizar la información del viaje.");
            throw;
        }
    }

    public async Task RefreshPackingAsync(HomeViewModel state, CancellationToken cancellationToken)
    {
        if (state.SelectedTripId == Guid.Empty || state.SelectedProfileId == Guid.Empty)
        {
            return;
        }

        state.PackingStatus.Begin();
        try
        {
            await LoadPackingCoreAsync(state, cancellationToken);
            RefreshPackingInsights(state);
            state.PackingStatus.Complete();
        }
        catch
        {
            state.PackingStatus.Fail("No se ha podido cargar esta maleta.");
            throw;
        }
    }

    public Task<GarmentRecognitionUsageResult> RefreshAiUsageAsync(CancellationToken cancellationToken) =>
        api.GetAiUsageAsync(cancellationToken);

    private void RefreshPackingInsights(HomeViewModel state)
    {
        try
        {
            state.PackingInsights = PackingInsightsService.Analyze(state.Plan, state.FamilyPlans, state.Wardrobe, state.Weather);
        }
        catch (Exception exception)
        {
            // Insights are optional advice. A malformed legacy garment must not hide a usable packing list.
            LogPackingInsightsFailure(logger, state.SelectedTripId, exception);
            state.PackingInsights = new([], [], [], [], []);
        }
    }

    private async Task LoadPackingCoreAsync(HomeViewModel state, CancellationToken cancellationToken)
    {
        if (state.SelectedTripId == Guid.Empty || state.SelectedProfileId == Guid.Empty)
        {
            state.Plan = null;
            state.LuggageRules = null;
            state.Checklist = [];
            return;
        }

        // Both endpoints can initialise a profile list. Create or retrieve it first so
        // the initial visit for a traveller cannot issue competing create requests.
        state.Plan = await api.GetProfilePackingListAsync(state.SelectedTripId, state.SelectedProfileId, cancellationToken);
        var rules = api.GetLuggageRulesAsync(state.SelectedTripId, state.SelectedProfileId, cancellationToken);
        var checklist = api.GetChecklistAsync(state.SelectedTripId, state.SelectedProfileId, cancellationToken);
        await Task.WhenAll(rules, checklist);
        state.LuggageRules = await rules;
        state.Checklist = await checklist;
    }
}
