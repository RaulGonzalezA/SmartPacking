using SmartPacking.Domain;

namespace SmartPacking.Application;

/// <summary>Read model required to render a trip without issuing one request per panel.</summary>
public sealed record TripDashboard(
    IReadOnlyList<FamilyProfile> Profiles,
    Guid SelectedProfileId,
    ProfileTripPackingPlan? SelectedPlan,
    IReadOnlyList<ProfileTripPackingPlan> FamilyPlans,
    IReadOnlyList<ChecklistItem> SelectedChecklist,
    IReadOnlyList<PreparationProgressItem> PreparationProgress,
    LuggageRulesSummary? LuggageRules,
    IReadOnlyList<ClothingUsage> Usage,
    TripWeatherForecast? Weather,
    string? WeatherFeedback,
    PackingRecommendationDiff? RecommendationDiff = null);
