using SmartPacking.Application;
using SmartPacking.Domain;

#pragma warning disable S3881

namespace SmartPacking.Web.Components.Pages;

public partial class Home
{
    private Task SetPackedAsync(SetPackingItemStatusCommand command) => RunAsync(async () =>
    {
        if (State.Plan is not null)
        {
            await Api.SetProfilePackedAsync(State.Plan.Plan.PackingListId, command.GarmentId, command.IsPacked, CancellationToken.None);
            await RefreshPackingAsync();
        }
    });

    private Task AddManualClothingAsync(Guid id) => RunAsync(async () =>
    {
        if (State.Plan is null)
        {
            State.Feedback = "Selecciona una prenda para añadirla.";
            return;
        }

        await Api.AddProfilePackingListItemAsync(State.Plan.Plan.PackingListId, id, CancellationToken.None);
        await RefreshPackingAsync();
    });

    private Task ResolveRecommendationChangeAsync(ResolvePackingRecommendationChangeCommand command) => RunAsync(async () =>
    {
        if (State.Plan is null)
        {
            return;
        }

        if (command.Apply)
        {
            await Api.ApplyProfileRecommendationChangeAsync(State.Plan.Plan.PackingListId, command.ClothingItemId, command.ChangeKind, CancellationToken.None);
        }
        else
        {
            await Api.IgnoreProfileRecommendationChangeAsync(State.Plan.Plan.PackingListId, command.ClothingItemId, command.ChangeKind, CancellationToken.None);
        }

        await RefreshTripDetailsAsync();
    });

    private Task AddToiletryAsync(AddChecklistItemCommand command) => RunAsync(async () =>
    {
        if (State.SelectedTripId == Guid.Empty || State.SelectedProfileId == Guid.Empty || string.IsNullOrWhiteSpace(command.Name))
        {
            return;
        }

        await Api.AddProfileChecklistItemAsync(State.SelectedTripId, State.SelectedProfileId, command.Category, command.Name.Trim(), CancellationToken.None);
        await RefreshPackingAsync();
    });

    private Task SetChecklistPackedAsync(SetChecklistItemStatusCommand command) => RunAsync(async () =>
    {
        await Api.SetChecklistPackedAsync(command.ChecklistItemId, command.IsPacked, CancellationToken.None);
        await RefreshTripDetailsAsync();
    });

    private Task SaveUsageAsync(IReadOnlyCollection<Guid> usedIds) => RunAsync(async () =>
    {
        await Api.SaveUsageAsync(State.SelectedTripId, State.UsageItemIds.Select(id => new ClothingUsage(State.SelectedTripId, id, usedIds.Contains(id))).ToArray(), CancellationToken.None);
        State.Feedback = "Uso real guardado.";
    });
}
