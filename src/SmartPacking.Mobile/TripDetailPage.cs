using SmartPacking.Application;
using SmartPacking.Client;
using SmartPacking.Domain;

namespace SmartPacking.Mobile;

public sealed class TripDetailPage : ContentPage
{
    private readonly ISmartPackingClient client;
    private readonly Trip trip;
    private readonly VerticalStackLayout content;
    private bool loading;

    public TripDetailPage(ISmartPackingClient client, Trip trip)
    {
        this.client = client;
        this.trip = trip;
        Title = trip.Destination;
        content = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        Content = new ScrollView { Content = content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
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
            content.Clear();
            content.Add(new Label { Text = trip.Destination, FontSize = 28, FontAttributes = FontAttributes.Bold });
            content.Add(new Label { Text = $"{trip.StartDate:d} – {trip.EndDate:d}" });

            var dashboard = await client.GetTripDashboardAsync(trip.Id, null, CancellationToken.None);
            if (dashboard is null)
            {
                content.Add(new Label { Text = "No se pudo cargar la preparación del viaje." });
                return;
            }

            AddWeather(dashboard);
            AddProgress(dashboard);
            AddChecklist(dashboard.SelectedChecklist);
        }
        catch (HttpRequestException exception)
        {
            content.Add(new Label { Text = $"No se pudo cargar el viaje: {exception.Message}" });
        }
        finally
        {
            loading = false;
        }
    }

    private void AddWeather(TripDashboard dashboard)
    {
        content.Add(new Label { Text = "Previsión", FontSize = 20, FontAttributes = FontAttributes.Bold });
        if (dashboard.Weather is null)
        {
            content.Add(new Label { Text = dashboard.WeatherFeedback ?? "Previsión todavía no disponible." });
            return;
        }

        content.Add(new Label
        {
            Text = $"{dashboard.Weather.MinimumCelsius} °C – {dashboard.Weather.MaximumCelsius} °C · lluvia máx. {dashboard.Weather.RainProbability}%"
        });
    }

    private void AddProgress(TripDashboard dashboard)
    {
        content.Add(new Label { Text = "Preparación", FontSize = 20, FontAttributes = FontAttributes.Bold });
        foreach (var progress in dashboard.PreparationProgress)
        {
            content.Add(new Label
            {
                Text = $"{progress.Name}: maleta {progress.PackedClothing}/{progress.TotalClothing} · checklist {progress.PackedChecklist}/{progress.TotalChecklist} · {progress.Percent}%"
            });
        }
    }

    private void AddChecklist(IReadOnlyList<ChecklistItem> checklist)
    {
        content.Add(new Label { Text = "Checklist", FontSize = 20, FontAttributes = FontAttributes.Bold });
        if (checklist.Count == 0)
        {
            content.Add(new Label { Text = "No hay elementos pendientes." });
            return;
        }

        foreach (var item in checklist)
        {
            var checkBox = new CheckBox { IsChecked = item.IsPacked };
            var label = new Label { Text = item.Name, VerticalTextAlignment = TextAlignment.Center };
            checkBox.CheckedChanged += async (_, args) => await SetChecklistAsync(item, args.Value, checkBox);
            content.Add(new HorizontalStackLayout { Spacing = 10, Children = { checkBox, label } });
        }
    }

    private async Task SetChecklistAsync(ChecklistItem item, bool isPacked, CheckBox checkBox)
    {
        try
        {
            checkBox.IsEnabled = false;
            await client.SetChecklistPackedAsync(item.Id, isPacked, CancellationToken.None);
        }
        catch (HttpRequestException)
        {
            checkBox.IsChecked = !isPacked;
            await DisplayAlertAsync("SmartPacking", "No se pudo actualizar el checklist. Inténtalo de nuevo.", "Aceptar");
        }
        finally
        {
            checkBox.IsEnabled = true;
        }
    }
}
