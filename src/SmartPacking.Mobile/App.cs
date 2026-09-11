using Microsoft.Extensions.DependencyInjection;

namespace SmartPacking.Mobile;

public sealed class App(IServiceProvider services) : Microsoft.Maui.Controls.Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(services.GetRequiredService<LoginPage>()));
}
