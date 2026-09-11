using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Client;

namespace SmartPacking.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        var options = MobileOptions.Current;
        options.Validate();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<SecureAccessTokenProvider>();
        builder.Services.AddSingleton<IAccessTokenProvider>(provider => provider.GetRequiredService<SecureAccessTokenProvider>());
        builder.Services.AddSingleton<IMobileAuthenticationService, MobileAuthenticationService>();
        builder.Services.AddSingleton<IAuthenticationRequiredHandler, MobileAuthenticationRequiredHandler>();
        builder.Services.AddSingleton<IMobilePhotoService, MobilePhotoService>();
        builder.Services.AddSingleton<ISmartPackingClient>(provider =>
        {
            var bearer = new BearerTokenHandler(
                provider.GetRequiredService<IAccessTokenProvider>(),
                provider.GetRequiredService<IAuthenticationRequiredHandler>())
            {
                InnerHandler = new HttpClientHandler()
            };
            var client = new HttpClient(bearer) { BaseAddress = new Uri(options.ApiBaseAddress) };
            return new SmartPackingClient(client);
        });
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<TripsPage>();
        builder.Services.AddTransient<WardrobePage>();
        builder.Services.AddTransient<AddGarmentPage>();

        return builder.Build();
    }
}
