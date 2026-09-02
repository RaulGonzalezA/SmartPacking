using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Application;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSmartPackingPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SmartPacking") ?? "Data Source=smartpacking.db";
        services.AddDbContext<SmartPackingDbContext>(options =>
        {
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure();
                    npgsqlOptions.MigrationsAssembly("SmartPacking.Infrastructure.PostgreSql");
                });
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });
        services.AddScoped<ISmartPackingStore, EfSmartPackingStore>();

        return services;
    }
}
