using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;

namespace SmartPacking.Api.DependencyInjection;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddSmartPackingPhotoStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var blobConnectionString = configuration["Storage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(blobConnectionString))
        {
            services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
        }
        else
        {
            services.AddSingleton(new BlobServiceClient(blobConnectionString));
            services.AddSingleton<IPhotoStorage, BlobPhotoStorage>();
        }

        return services;
    }
}
