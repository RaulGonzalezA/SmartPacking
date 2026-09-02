using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.DependencyInjection;

public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddSmartPackingHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<SmartPackingDbContext>("database", tags: ["ready"])
            .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

        return services;
    }
}
