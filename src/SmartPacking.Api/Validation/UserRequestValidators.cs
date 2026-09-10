using FluentValidation;
using SmartPacking.Api;
using SmartPacking.Contracts;
using SmartPacking.Domain;

namespace SmartPacking.Api.Validation;

public sealed class CompleteUserOnboardingRequestValidator : AbstractValidator<CompleteUserOnboardingRequest>
{
    public CompleteUserOnboardingRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class SaveTripRequestValidator : AbstractValidator<SaveTripRequest>
{
    public SaveTripRequestValidator()
    {
        RuleFor(request => request.Destination).NotEmpty().MaximumLength(120);
        RuleFor(request => request.EndDate).GreaterThanOrEqualTo(request => request.StartDate);
        RuleFor(request => request.MinimumTemperatureCelsius).InclusiveBetween(-80, 60);
        RuleFor(request => request.MaximumTemperatureCelsius)
            .InclusiveBetween(-80, 70)
            .GreaterThanOrEqualTo(request => request.MinimumTemperatureCelsius);
        RuleForEach(request => request.Activities).Must(activity => Enum.IsDefined((Style)activity));
        RuleFor(request => request.Origin).MaximumLength(160);
        RuleFor(request => request.AirlineCode).MaximumLength(12);
        RuleFor(request => request.LuggageAllowanceGrams).GreaterThanOrEqualTo(0).When(request => request.LuggageAllowanceGrams.HasValue);
        RuleFor(request => request.LuggageHeightCentimetres).GreaterThan(0).When(request => request.LuggageHeightCentimetres.HasValue);
        RuleFor(request => request.LuggageWidthCentimetres).GreaterThan(0).When(request => request.LuggageWidthCentimetres.HasValue);
        RuleFor(request => request.LuggageDepthCentimetres).GreaterThan(0).When(request => request.LuggageDepthCentimetres.HasValue);
        RuleFor(request => request.LuggageType).Must(type => !type.HasValue || Enum.IsDefined((LuggageType)type.Value));
        RuleForEach(request => request.TransportTypes).Must(type => Enum.IsDefined((TransportType)type));
        RuleFor(request => request.Luggages).Must(luggages => luggages is null || luggages.Count > 0);
        RuleForEach(request => request.Luggages).ChildRules(luggage =>
        {
            luggage.RuleFor(item => item.Type).Must(type => Enum.IsDefined((LuggageType)type));
            luggage.RuleFor(item => item.AllowanceGrams).GreaterThanOrEqualTo(0);
            luggage.RuleFor(item => item.HeightCentimetres).GreaterThan(0);
            luggage.RuleFor(item => item.WidthCentimetres).GreaterThan(0);
            luggage.RuleFor(item => item.DepthCentimetres).GreaterThan(0);
        });
        RuleForEach(request => request.DayPlans).Must((request, plan) => plan.Date >= request.StartDate && plan.Date <= request.EndDate)
            .WithMessage("La fecha de cada actividad debe estar dentro del viaje.");
        RuleForEach(request => request.DayPlans).ChildRules(dayPlan =>
        {
            dayPlan.RuleFor(plan => plan.Activities).Must(activities => activities.Count is >= 1 and <= 3);
            dayPlan.RuleForEach(plan => plan.Activities).Must(activity => Enum.IsDefined((TripActivity)activity));
        });
    }
}

public sealed class UpdateCurrentUserRequestValidator : AbstractValidator<UpdateCurrentUserRequest>
{
    public UpdateCurrentUserRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Street).MaximumLength(80);
        RuleFor(request => request.PostalCode).MaximumLength(12);
        RuleFor(request => request.City).MaximumLength(60);
        RuleFor(request => request.Region).MaximumLength(80);
        When(request => HasAddressData(request), () => RuleFor(request => request.City).NotEmpty().WithMessage("La población es obligatoria cuando se informa una dirección."));
    }

    private static bool HasAddressData(UpdateCurrentUserRequest request) =>
        !string.IsNullOrWhiteSpace(request.Street) || !string.IsNullOrWhiteSpace(request.PostalCode) || !string.IsNullOrWhiteSpace(request.City) || !string.IsNullOrWhiteSpace(request.Region);
}
