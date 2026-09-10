using Microsoft.AspNetCore.Components.Forms;
using SmartPacking.Domain;

namespace SmartPacking.Web.Components;

public sealed record CreateGarmentCommand(
    string Name,
    string Color,
    Guid OwnerId,
    ClothingType Type,
    Season Season,
    Style Style,
    string? Material,
    int WeightGrams,
    IBrowserFile? Photo);

public sealed record UpdateGarmentStatusCommand(Guid GarmentId, bool IsClean, bool IsAvailable);

public sealed record UploadGarmentPhotoCommand(Guid GarmentId, IBrowserFile Photo);

public sealed record SetPackingItemStatusCommand(Guid GarmentId, bool IsPacked);

public sealed record SetChecklistItemStatusCommand(Guid ChecklistItemId, bool IsPacked);

public sealed record AddChecklistItemCommand(string Name, ChecklistCategory Category);
