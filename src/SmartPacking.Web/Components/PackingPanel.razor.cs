using Microsoft.AspNetCore.Components;
using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Web.Components;

public partial class PackingPanel
{
    private Guid manualClothingItemId;
    private readonly HashSet<ClothingType> dismissedMissing = [];

    [Parameter]
    public bool IsActive { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public string? Feedback { get; set; }

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
    public EventCallback<SetPackingItemStatusCommand> PackedChanged { get; set; }

    [Parameter]
    public EventCallback<Guid> ManualClothingAdded { get; set; }

    [Parameter]
    public EventCallback<SetChecklistItemStatusCommand> ChecklistPackedChanged { get; set; }

    [Parameter]
    public EventCallback<AddChecklistItemCommand> ToiletryAdded { get; set; }

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

    private void MarkMissing(ClothingType type) => dismissedMissing.Add(type);
    private static string MissingIcon(ClothingType type) => type switch
    {
        ClothingType.Socks => "🧦",
        ClothingType.Underwear => "🩲",
        ClothingType.Shoes or ClothingType.Sandals => "👟",
        ClothingType.Swimwear => "🩱",
        ClothingType.Jacket or ClothingType.Coat => "🧥",
        _ => "✦"
    };

    private IReadOnlyList<PreparationTask> PriorityTasks
    {
        get
        {
            if (Plan is null)
            {
                return [];
            }

            var tasks = new List<PreparationTask>();
            foreach (var item in Plan.Plan.Items.Where(item => !item.IsPacked).Take(3))
            {
                tasks.Add(new("Prenda pendiente", $"Añade {item.Recommendation.Item.Name} a la maleta.", "👕"));
            }

            foreach (var item in Checklist.Where(item => !item.IsPacked).Take(3 - tasks.Count))
            {
                tasks.Add(new("Imprescindible", $"Revisa: {item.Name}.", "✓"));
            }

            return tasks;
        }
    }

    private sealed record PreparationTask(string Label, string Description, string Icon);
}
