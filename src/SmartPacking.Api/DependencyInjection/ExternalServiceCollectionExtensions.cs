using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.DependencyInjection;

public static class ExternalServiceCollectionExtensions
{
    public static IServiceCollection AddSmartPackingExternalServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<OpenMeteoWeatherProvider>(client => client.Timeout = TimeSpan.FromSeconds(10))
            .AddStandardResilienceHandler();
        var geminiBaseUrl = configuration["Gemini:BaseUrl"] ?? throw new InvalidOperationException("Gemini:BaseUrl es obligatoria.");
        services.AddHttpClient<IGarmentRecognizer, GeminiGarmentRecognizer>(client =>
            {
                client.BaseAddress = new Uri(new Uri(geminiBaseUrl), "/");
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .AddStandardResilienceHandler();

        return services;
    }
}
