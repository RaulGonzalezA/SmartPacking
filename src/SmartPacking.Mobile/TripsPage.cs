using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Client;
using SmartPacking.Domain;

namespace SmartPacking.Mobile;

public sealed class TripsPage : ContentPage
{
    private readonly ISmartPackingClient client;
    private readonly IMobileAuthenticationService authentication;
    private readonly IServiceProvider services;
    private readonly VerticalStackLayout tripList;
    private bool loading;

    public TripsPage(ISmartPackingClient client, IMobileAuthenticationService authentication, IServiceProvider services)
    {
        this.client = client;
        this.authentication = authentication;
        this.services = services;
        Title = "Mis viajes";

        var refreshButton = new Button { Text = "Actualizar" };
        refreshButton.Clicked += RefreshClicked;
        var logoutButton = new Button { Text = "Cerrar sesión" };
        logoutButton.Clicked += LogoutClicked;
        tripList = new VerticalStackLayout { Spacing = 12 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 16,
                Children =
                {
                    new Label { Text = "Mis viajes", FontSize = 28, FontAttributes = FontAttributes.Bold },
                    new HorizontalStackLayout { Spacing = 10, Children = { refreshButton, logoutButton } },
                    tripList
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void RefreshClicked(object? sender, EventArgs e) => await LoadAsync();

    private async void LogoutClicked(object? sender, EventArgs e)
    {
        await authentication.LogoutAsync();
        var loginPage = services.GetRequiredService<LoginPage>();
        await Navigation.PushAsync(loginPage);
        Navigation.RemovePage(this);
    }

    private async Task LoadAsync()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        try
        {
            tripList.Clear();
            var trips = await client.GetTripsAsync(CancellationToken.None);
            if (trips.Count == 0)
            {
                tripList.Add(new Label { Text = "Todavía no tienes viajes." });
                return;
            }

            foreach (var trip in trips.OrderBy(item => item.StartDate))
            {
                tripList.Add(CreateTripCard(trip));
            }
        }
        catch (HttpRequestException exception)
        {
            tripList.Clear();
            tripList.Add(new Label { Text = $"No se pudieron cargar los viajes: {exception.Message}" });
        }
        finally
        {
            loading = false;
        }
    }

    private View CreateTripCard(Trip trip)
    {
        var button = new Button
        {
            Text = $"{trip.Destination}\n{trip.StartDate:d} – {trip.EndDate:d}",
            HorizontalOptions = LayoutOptions.Fill
        };
        button.Clicked += async (_, _) => await Navigation.PushAsync(new TripDetailPage(client, trip));
        return button;
    }
}
