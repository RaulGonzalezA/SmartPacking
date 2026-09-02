using Microsoft.Extensions.DependencyInjection;

namespace SmartPacking.Api.DependencyInjection;

public static class CacheServiceCollectionExtensions
{
    public static IServiceCollection AddSmartPackingCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration["Cache:RedisConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
        }

        return services;
    }
}
