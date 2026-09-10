using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Client;

namespace SmartPacking.Mobile;

public sealed class LoginPage : ContentPage
{
    private readonly IMobileAuthenticationService authentication;
    private readonly ISmartPackingClient client;
    private readonly IServiceProvider services;
    private readonly Label status;
    private bool initialized;

    public LoginPage(IMobileAuthenticationService authentication, ISmartPackingClient client, IServiceProvider services)
    {
        this.authentication = authentication;
        this.client = client;
        this.services = services;

        Title = "SmartPacking";
        status = new Label { Text = "Inicia sesión para ver tus viajes." };
        var loginButton = new Button { Text = "Iniciar sesión" };
        loginButton.Clicked += LoginClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 18,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = "SmartPacking", FontSize = 32, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Tu maleta inteligente, también en el móvil.", FontSize = 18 },
                    status,
                    loginButton
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (initialized)
        {
            return;
        }

        initialized = true;
        try
        {
            if (await authentication.HasSessionAsync(CancellationToken.None))
            {
                await client.GetCurrentUserAsync(CancellationToken.None);
                await OpenTripsAsync();
            }
        }
        catch (HttpRequestException)
        {
            await authentication.LogoutAsync();
            status.Text = "La sesión guardada ya no es válida. Vuelve a iniciar sesión.";
        }
    }

    private async void LoginClicked(object? sender, EventArgs e)
    {
        try
        {
            status.Text = "Abriendo Auth0…";
            await authentication.LoginAsync(CancellationToken.None);
            await client.GetCurrentUserAsync(CancellationToken.None);
            await OpenTripsAsync();
        }
        catch (InvalidOperationException exception)
        {
            status.Text = exception.Message;
        }
        catch (HttpRequestException exception)
        {
            status.Text = $"No se pudo completar el inicio de sesión: {exception.Message}";
        }
        catch (TaskCanceledException)
        {
            status.Text = "Inicio de sesión cancelado.";
        }
    }

    private async Task OpenTripsAsync()
    {
        var tripsPage = services.GetRequiredService<TripsPage>();
        await Navigation.PushAsync(tripsPage);
        Navigation.RemovePage(this);
    }
}
