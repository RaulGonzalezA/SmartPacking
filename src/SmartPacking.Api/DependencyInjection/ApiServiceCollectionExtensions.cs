using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using SmartPacking.Api.Contracts;
using SmartPacking.Api.Controllers;
using SmartPacking.Api.Validation;
using SmartPacking.Contracts;

namespace SmartPacking.Api.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddSmartPackingApi(this IServiceCollection services)
    {
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddControllers();
        services.AddProblemDetails();
        services.AddOpenApi();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddScoped<IValidator<CompleteUserOnboardingRequest>, CompleteUserOnboardingRequestValidator>();
        services.AddScoped<IValidator<UpdateCurrentUserRequest>, UpdateCurrentUserRequestValidator>();
        services.AddScoped<IValidator<SaveTripRequest>, SaveTripRequestValidator>();
        services.AddScoped<IValidator<CreateFamilyProfileRequest>, CreateFamilyProfileRequestValidator>();
        services.AddScoped<IValidator<UpdateFamilyProfileRequest>, UpdateFamilyProfileRequestValidator>();
        services.AddScoped<IValidator<CreateChecklistItemRequest>, CreateChecklistItemRequestValidator>();
        services.AddScoped<IValidator<SetTripProfilesRequest>, SetTripProfilesRequestValidator>();
        services.AddScoped<IValidator<SaveUserTripTemplateRequest>, SaveUserTripTemplateRequestValidator>();
        services.AddScoped<IValidator<UpsertClothingItemRequest>, UpsertClothingItemRequestValidator>();
        services.AddScoped<IValidator<DeleteCurrentUserRequest>, DeleteCurrentUserRequestValidator>();
        services.AddScoped<IValidator<SetPlanRequest>, SetPlanRequestValidator>();
        services.AddScoped<IValidator<AddCreditsRequest>, AddCreditsRequestValidator>();

        return services;
    }
}
