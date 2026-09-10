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
            var wardrobe = await api.GetWardrobeCollectionAsync(cancellationToken);
            state.Wardrobe = wardrobe.Items;
            state.DeletedWardrobe = wardrobe.DeletedItems;
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
            var dashboard = await api.GetTripDashboardAsync(state.SelectedTripId, state.SelectedProfileId, cancellationToken);
            if (dashboard is null)
            {
                state.ClearSelectedTripData();
                state.PackingStatus.Complete();
                return;
            }

            state.TripProfiles = dashboard.Profiles;
            state.SelectProfile(dashboard.SelectedProfileId);
            state.Plan = dashboard.SelectedPlan;
            state.FamilyPlans = dashboard.FamilyPlans;
            state.Checklist = dashboard.SelectedChecklist;
            state.PreparationProgress = dashboard.PreparationProgress;
            state.LuggageRules = dashboard.LuggageRules;
            state.Weather = dashboard.Weather;
            state.WeatherFeedback = dashboard.WeatherFeedback;
            state.RecommendationDiff = dashboard.RecommendationDiff;
            state.UsageItemIds = dashboard.Usage.Count == 0
                ? state.Plan?.Plan.Items.Select(item => item.Recommendation.Item.Id).ToHashSet() ?? []
                : dashboard.Usage.Select(item => item.ClothingItemId).ToHashSet();
            state.UsedItemIds = dashboard.Usage.Where(item => item.WasUsed).Select(item => item.ClothingItemId).ToHashSet();
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

    public async Task RefreshWeatherAsync(HomeViewModel state, CancellationToken cancellationToken)
    {
        state.Weather = null;
        state.WeatherFeedback = null;
        try
        {
            state.Weather = await api.GetWeatherAsync(state.SelectedTripId, cancellationToken);
        }
        catch (Exception exception)
        {
            var result = ApiOperationResult.FromException(exception);
            if (result.Status is ApiOperationStatus.NotFound or ApiOperationStatus.ValidationError or ApiOperationStatus.TransientFailure)
            {
                state.WeatherFeedback = result.Message;
                return;
            }

            throw;
        }
    }

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
