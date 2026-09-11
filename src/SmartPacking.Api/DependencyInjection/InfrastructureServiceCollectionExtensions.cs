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
        var provider = configuration["Persistence:Provider"] ?? InferProvider(connectionString);
        services.AddDbContext<SmartPackingDbContext>(options =>
        {
            if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure();
                    npgsqlOptions.MigrationsAssembly("SmartPacking.Infrastructure.PostgreSql");
                });
            }
            else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString, sqlServerOptions =>
                {
                    sqlServerOptions.EnableRetryOnFailure();
                    sqlServerOptions.MigrationsAssembly("SmartPacking.Infrastructure.SqlServer");
                });
            }
            else if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                throw new InvalidOperationException("Persistence:Provider debe ser SqlServer, PostgreSql o Sqlite.");
            }
        });
        services.AddScoped<ISmartPackingStore, EfSmartPackingStore>();
        services.AddScoped<IClothingItemLookup, EfClothingItemLookup>();
        services.AddScoped<IGarmentRecognitionUsageService, GarmentRecognitionUsageService>();

        return services;
    }

    private static string InferProvider(string connectionString)
    {
        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        {
            return "PostgreSql";
        }

        if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
        {
            return "SqlServer";
        }

        return "Sqlite";
    }
}
