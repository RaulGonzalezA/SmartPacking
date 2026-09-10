using Microsoft.AspNetCore.Components.Forms;
using SmartPacking.Application;
using SmartPacking.Domain;

#pragma warning disable S3881, S8949

namespace SmartPacking.Web.Components.Pages;

public partial class Home
{
    private Task CreateClothingAsync(CreateGarmentCommand command) => RunAsync(async () =>
    {
        var item = await Api.CreateClothingAsync(new ClothingItem(Guid.NewGuid(), command.Name, command.Type, command.Season, command.Color, 2, false, command.Style, command.WeightGrams, true, true, 70, [], false, command.OwnerId, null, command.Material), CancellationToken.None);
        if (command.Photo is not null)
        {
            await UploadClothingPhotoCoreAsync(item.Id, command.Photo);
        }

        State.Feedback = "Prenda guardada.";
        await RefreshWardrobeAsync();
    });

    private Task UploadClothingPhotoAsync(UploadGarmentPhotoCommand command) => RunAsync(() => UploadClothingPhotoCoreAsync(command.GarmentId, command.Photo));

    private async Task UploadClothingPhotoCoreAsync(Guid id, IBrowserFile file)
    {
        await using var content = file.OpenReadStream(5 * 1024 * 1024);
        var url = await Api.UploadClothingPhotoAsync(id, content, file.ContentType, file.Name, CancellationToken.None);
        wardrobePanel?.SetPhotoUrl(id, url);
        State.Feedback = "Foto de la prenda actualizada.";
    }

    private async Task<GarmentRecognitionSuggestion> RecognizeGarmentAsync(IBrowserFile file)
    {
        await using var content = file.OpenReadStream(5 * 1024 * 1024);
        var suggestion = await Api.RecognizeGarmentAsync(content, file.ContentType, file.Name, lifetimeCancellation.Token);
        aiUsage = await DataCoordinator.RefreshAiUsageAsync(lifetimeCancellation.Token);
        return suggestion;
    }

    private Task UpdateStatusAsync(UpdateGarmentStatusCommand command) => RunAsync(async () =>
    {
        await Api.UpdateClothingStatusAsync(command.GarmentId, command.IsClean, command.IsAvailable, CancellationToken.None);
        await RefreshWardrobeAsync();
    });

    private async Task DeleteClothingAsync(Guid id)
    {
        await Api.DeleteClothingAsync(id, CancellationToken.None);
        await RefreshWardrobeAsync();
    }

    private async Task RestoreClothingAsync(Guid id)
    {
        await Api.RestoreClothingAsync(id, CancellationToken.None);
        await RefreshWardrobeAsync();
    }
}
