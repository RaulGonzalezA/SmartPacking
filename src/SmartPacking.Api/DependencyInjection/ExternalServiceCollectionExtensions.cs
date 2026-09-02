using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.DependencyInjection;

public static class ExternalServiceCollectionExtensions
{
    public static IServiceCollection AddSmartPackingExternalServices(this IServiceCollection services)
    {
        services.AddHttpClient<OpenMeteoWeatherProvider>(client => client.Timeout = TimeSpan.FromSeconds(10))
            .AddStandardResilienceHandler();

        return services;
    }
}
