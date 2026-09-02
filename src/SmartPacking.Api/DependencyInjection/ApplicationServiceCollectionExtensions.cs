using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Application;

namespace SmartPacking.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSmartPackingApplication(this IServiceCollection services)
    {
        services.AddScoped<PackingListService>();
        services.AddScoped<ProfilePackingListService>();

        return services;
    }
}
