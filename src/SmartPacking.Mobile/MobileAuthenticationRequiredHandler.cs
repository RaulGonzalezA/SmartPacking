using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using SmartPacking.Client;

namespace SmartPacking.Mobile;

public sealed class MobileAuthenticationRequiredHandler(IServiceProvider services) : IAuthenticationRequiredHandler
{
    private int navigationInProgress;

    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        SecureAccessTokenProvider.Clear();
        if (Interlocked.CompareExchange(ref navigationInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var application = Microsoft.Maui.Controls.Application.Current;
                var window = application is null || application.Windows.Count == 0
                    ? null
                    : application.Windows[0];
                if (window?.Page is not NavigationPage navigation || navigation.CurrentPage is LoginPage)
                {
                    return;
                }

                var loginPage = services.GetRequiredService<LoginPage>();
                await navigation.PushAsync(loginPage);
                foreach (var page in navigation.Navigation.NavigationStack
                    .Where(page => !ReferenceEquals(page, loginPage))
                    .ToArray())
                {
                    navigation.Navigation.RemovePage(page);
                }
            });
        }
        finally
        {
            _ = Interlocked.Exchange(ref navigationInProgress, 0);
        }
    }
}
