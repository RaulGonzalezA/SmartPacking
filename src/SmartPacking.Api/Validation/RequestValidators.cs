using FluentValidation;
using SmartPacking.Api.Contracts;
using SmartPacking.Api.Controllers;
using SmartPacking.Domain;

namespace SmartPacking.Api.Validation;

public sealed class CreateFamilyProfileRequestValidator : AbstractValidator<CreateFamilyProfileRequest>
{
    public CreateFamilyProfileRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(80);
        RuleFor(request => request.PackingNotes).MaximumLength(1_000);
        RuleFor(request => request.MedicalNotes).MaximumLength(1_000);
    }
}

public sealed class UpdateFamilyProfileRequestValidator : AbstractValidator<UpdateFamilyProfileRequest>
{
    public UpdateFamilyProfileRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(80);
        RuleFor(request => request.PackingNotes).MaximumLength(1_000);
        RuleFor(request => request.MedicalNotes).MaximumLength(1_000);
    }
}

public sealed class CreateChecklistItemRequestValidator : AbstractValidator<CreateChecklistItemRequest>
{
    public CreateChecklistItemRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Category).IsInEnum();
    }
}

public sealed class SetTripProfilesRequestValidator : AbstractValidator<SetTripProfilesRequest>
{
    public SetTripProfilesRequestValidator()
    {
        RuleFor(request => request.ProfileIds).NotNull();
        RuleForEach(request => request.ProfileIds).NotEmpty();
    }
}

public sealed class DeleteCurrentUserRequestValidator : AbstractValidator<DeleteCurrentUserRequest>
{
    public DeleteCurrentUserRequestValidator()
    {
        RuleFor(request => request.Confirmation).Equal("ELIMINAR");
    }
}

public sealed class SetPlanRequestValidator : AbstractValidator<SetPlanRequest>
{
    public SetPlanRequestValidator()
    {
        RuleFor(request => request.Plan).Must(plan => plan is "Free" or "Premium");
    }
}

public sealed class AddCreditsRequestValidator : AbstractValidator<AddCreditsRequest>
{
    public AddCreditsRequestValidator()
    {
        RuleFor(request => request.Credits).InclusiveBetween(1, 10_000);
    }
}

public sealed class SaveUserTripTemplateRequestValidator : AbstractValidator<SaveUserTripTemplateRequest>
{
    public SaveUserTripTemplateRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.MaximumTemperatureCelsius).GreaterThanOrEqualTo(request => request.MinimumTemperatureCelsius);
        RuleFor(request => request.LuggageAllowanceGrams).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000);
        RuleForEach(request => request.Activities).IsInEnum();
    }
}

public sealed class UpsertClothingItemRequestValidator : AbstractValidator<UpsertClothingItemRequest>
{
    public UpsertClothingItemRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Type).IsInEnum();
        RuleFor(request => request.Season).IsInEnum();
        RuleFor(request => request.Color).NotEmpty().MaximumLength(50);
        RuleFor(request => request.WarmthLevel).InclusiveBetween(1, 10);
        RuleFor(request => request.Style).IsInEnum();
        RuleFor(request => request.WeightGrams).InclusiveBetween(1, 20_000).When(request => request.WeightGrams.HasValue);
        RuleFor(request => request.PreferenceScore).InclusiveBetween(0, 100);
        RuleFor(request => request.Material).MaximumLength(80);
        RuleForEach(request => request.CombinesWith!).NotEmpty().When(request => request.CombinesWith is not null);
    }
}
