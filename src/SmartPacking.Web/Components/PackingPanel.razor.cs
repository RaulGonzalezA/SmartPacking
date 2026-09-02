using Microsoft.AspNetCore.Components;
using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Web.Components;

public partial class PackingPanel
{
    private Guid manualClothingItemId;

    [Parameter]
    public bool IsActive { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public ProfileTripPackingPlan? Plan { get; set; }

    [Parameter]
    public IReadOnlyList<FamilyProfile> TripProfiles { get; set; } = [];

    [Parameter]
    public Guid SelectedProfileId { get; set; }

    [Parameter]
    public IReadOnlyList<ClothingItem> Wardrobe { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ChecklistItem> Checklist { get; set; } = [];

    [Parameter]
    public IReadOnlyList<PreparationProgressItem> PreparationProgress { get; set; } = [];

    [Parameter]
    public LuggageRulesSummary? LuggageRules { get; set; }

    [Parameter]
    public PackingInsights? Insights { get; set; }

    [Parameter]
    public EventCallback<Guid> SelectedProfileChanged { get; set; }

    [Parameter]
    public EventCallback<(PlannedItem Item, bool IsPacked)> PackedChanged { get; set; }

    [Parameter]
    public EventCallback<Guid> ManualClothingAdded { get; set; }

    [Parameter]
    public EventCallback<(ChecklistItem Item, bool IsPacked)> ChecklistPackedChanged { get; set; }

    [Parameter]
    public EventCallback<string> ToiletryAdded { get; set; }

    private IEnumerable<ClothingItem> AvailableManualClothing => Plan is null
        ? []
        : Wardrobe.Where(item => !item.IsDeleted
            && (item.OwnerProfileId is null || item.OwnerProfileId == SelectedProfileId)
            && Plan.Plan.Items.All(planned => planned.Recommendation.Item.Id != item.Id));

    private Task SelectProfile(ChangeEventArgs args) => SelectedProfileChanged.InvokeAsync(
        Guid.TryParse(args.Value?.ToString(), out var id) ? id : Guid.Empty);

    private async Task AddManualClothing()
    {
        if (manualClothingItemId == Guid.Empty)
        {
            return;
        }

        await ManualClothingAdded.InvokeAsync(manualClothingItemId);
        manualClothingItemId = Guid.Empty;
    }
}
