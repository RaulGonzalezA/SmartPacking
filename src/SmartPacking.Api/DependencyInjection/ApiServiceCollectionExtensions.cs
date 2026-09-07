using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
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

        return services;
    }
}
