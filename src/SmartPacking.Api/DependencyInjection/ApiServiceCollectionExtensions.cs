using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
