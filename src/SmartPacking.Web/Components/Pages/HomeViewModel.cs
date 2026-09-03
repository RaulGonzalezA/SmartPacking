using SmartPacking.Application;
using SmartPacking.Domain;

#pragma warning disable IDE0011, CA1826

namespace SmartPacking.Web.Components.Pages;

public sealed class HomeViewModel
{
    public string ActiveTab { get; private set; } = "trips";
    public string? Feedback { get; set; }
    public IReadOnlyList<Trip> Trips { get; set; } = [];
    public IReadOnlyList<FamilyProfile> Profiles { get; set; } = [];
    public IReadOnlyList<TripTemplate> Templates { get; set; } = [];
    public IReadOnlyList<FamilyProfile> TripProfiles { get; set; } = [];
    public IReadOnlyList<ClothingItem> Wardrobe { get; set; } = [];
    public IReadOnlyList<ClothingItem> DeletedWardrobe { get; set; } = [];
    public ProfileTripPackingPlan? Plan { get; set; }
    public IReadOnlyList<ProfileTripPackingPlan> FamilyPlans { get; set; } = [];
    public PackingInsights? PackingInsights { get; set; }
    public TripWeatherForecast? Weather { get; set; }
    public LuggageRulesSummary? LuggageRules { get; set; }
    public IReadOnlyList<ChecklistItem> Checklist { get; set; } = [];
    public IReadOnlyList<PreparationProgressItem> PreparationProgress { get; set; } = [];
    public IReadOnlySet<Guid> UsageItemIds { get; set; } = new HashSet<Guid>();
    public IReadOnlySet<Guid> UsedItemIds { get; set; } = new HashSet<Guid>();
    public Guid SelectedTripId { get; private set; }
    public Guid SelectedProfileId { get; private set; }
    public bool IsLoading { get; set; }
    public bool IsSubmitting { get; set; }
    public bool IsBusy => IsLoading || IsSubmitting;
    public void SelectTab(string tab) => ActiveTab = tab;
    public void SelectTrip(Guid tripId) { SelectedTripId = tripId; Feedback = null; }
    public void SelectProfile(Guid profileId) => SelectedProfileId = profileId;
    public void SelectInitialTrip() { if (SelectedTripId == Guid.Empty) SelectedTripId = Trips.FirstOrDefault()?.Id ?? Guid.Empty; }
    public void EnsureSelectedProfile() { if (SelectedProfileId == Guid.Empty || !TripProfiles.Any(profile => profile.Id == SelectedProfileId)) SelectedProfileId = TripProfiles.FirstOrDefault()?.Id ?? Guid.Empty; }
    public void ClearSelectedTripData() { TripProfiles = []; Plan = null; FamilyPlans = []; PackingInsights = null; Weather = null; LuggageRules = null; Checklist = []; PreparationProgress = []; UsageItemIds = new HashSet<Guid>(); UsedItemIds = new HashSet<Guid>(); }
}
