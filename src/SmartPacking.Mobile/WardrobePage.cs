using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Client;
using SmartPacking.Domain;

namespace SmartPacking.Mobile;

public sealed class WardrobePage : ContentPage
{
    private const int PageSize = 20;
    private readonly ISmartPackingClient client;
    private readonly IServiceProvider services;
    private readonly CollectionView wardrobeView;
    private readonly Label statusLabel;
    private readonly PageCancellation pageCancellation = new();
    private readonly ObservableCollection<WardrobeItemViewModel> items = [];
    private bool loading;
    private bool hasMore = true;
    private int currentPage;

    public WardrobePage(ISmartPackingClient client, IServiceProvider services)
    {
        this.client = client;
        this.services = services;
        Title = "Armario";

        var addButton = new Button { Text = "Añadir prenda" };
        addButton.Clicked += AddGarmentClicked;
        var refreshButton = new Button { Text = "Actualizar" };
        refreshButton.Clicked += RefreshClicked;

        statusLabel = new Label { Text = "Cargando armario..." };
        wardrobeView = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 },
            ItemsSource = items,
            EmptyView = new Label { Text = "Todavía no tienes prendas en el armario.", Margin = new Thickness(0, 24) },
            ItemTemplate = new DataTemplate(CreateGarmentCard),
            RemainingItemsThreshold = 4
        };
        wardrobeView.RemainingItemsThresholdReached += RemainingItemsThresholdReached;
        wardrobeView.SelectionChanged += WardrobeSelectionChanged;

        var title = new Label
        {
            Text = "Mi armario",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        header.Add(title);
        header.Add(addButton);
        Grid.SetColumn(addButton, 1);

        var root = new Grid
        {
            Padding = 20,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };
        root.Add(header);
        root.Add(refreshButton);
        root.Add(statusLabel);
        root.Add(wardrobeView);
        Grid.SetRow(refreshButton, 1);
        Grid.SetRow(statusLabel, 2);
        Grid.SetRow(wardrobeView, 3);
        Content = root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        pageCancellation.Reset();
        await LoadNextPageAsync(reset: true, pageCancellation.Token);
    }

    protected override void OnDisappearing()
    {
        pageCancellation.Release();
        base.OnDisappearing();
    }

    private async void AddGarmentClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(services.GetRequiredService<AddGarmentPage>());

    private async void RefreshClicked(object? sender, EventArgs e) =>
        await LoadNextPageAsync(reset: true, pageCancellation.Token);

    private async void RemainingItemsThresholdReached(object? sender, EventArgs e) =>
        await LoadNextPageAsync(reset: false, pageCancellation.Token);

    private async void WardrobeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not WardrobeItemViewModel selected)
        {
            return;
        }

        wardrobeView.SelectedItem = null;
        var photoService = services.GetRequiredService<IMobilePhotoService>();
        await Navigation.PushAsync(new GarmentDetailPage(client, photoService, selected.Item));
    }

    private async Task LoadNextPageAsync(bool reset, CancellationToken cancellationToken)
    {
        if (loading || (!reset && !hasMore))
        {
            return;
        }

        loading = true;
        if (reset)
        {
            items.Clear();
            currentPage = 0;
            hasMore = true;
        }

        statusLabel.Text = currentPage == 0 ? "Cargando armario..." : "Cargando más prendas...";
        try
        {
            var nextPage = currentPage + 1;
            var page = await client.GetWardrobePageAsync(nextPage, PageSize, cancellationToken);
            using var gate = new SemaphoreSlim(4);
            var models = await Task.WhenAll(page.Items.Select(item => CreateViewModelAsync(item, gate, cancellationToken)));
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var model in models)
            {
                items.Add(model);
            }

            currentPage = nextPage;
            hasMore = page.HasMore;
            statusLabel.Text = hasMore
                ? $"{items.Count} prendas cargadas · sigue desplazándote para cargar más"
                : $"{items.Count} prendas";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            statusLabel.Text = "Carga cancelada.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            statusLabel.Text = $"No se pudo cargar el armario: {exception.Message}";
        }
        finally
        {
            loading = false;
        }
    }

    private async Task<WardrobeItemViewModel> CreateViewModelAsync(
        ClothingItem item,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        byte[]? photoBytes = null;
        if (!string.IsNullOrWhiteSpace(item.PhotoUrl))
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    photoBytes = await client.GetClothingThumbnailAsync(item.Id, cancellationToken);
                }
                catch (HttpRequestException)
                {
                    photoBytes = null;
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    photoBytes = null;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        return new WardrobeItemViewModel(item, photoBytes);
    }

    private static Grid CreateGarmentCard()
    {
        var image = new Image
        {
            HeightRequest = 96,
            WidthRequest = 96,
            Aspect = Aspect.AspectFill,
            HorizontalOptions = LayoutOptions.Start
        };
        image.SetBinding(Image.SourceProperty, nameof(WardrobeItemViewModel.Photo));

        var name = new Label { FontSize = 18, FontAttributes = FontAttributes.Bold };
        name.SetBinding(Label.TextProperty, nameof(WardrobeItemViewModel.Name));
        var description = new Label { FontSize = 14 };
        description.SetBinding(Label.TextProperty, nameof(WardrobeItemViewModel.Description));
        var status = new Label { FontSize = 13 };
        status.SetBinding(Label.TextProperty, nameof(WardrobeItemViewModel.Status));

        var text = new VerticalStackLayout
        {
            Spacing = 5,
            VerticalOptions = LayoutOptions.Center,
            Children = { name, description, status }
        };

        var card = new Grid
        {
            Padding = 10,
            ColumnSpacing = 14,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 106 },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        card.Add(image);
        card.Add(text);
        Grid.SetColumn(text, 1);
        return card;
    }

    private sealed class WardrobeItemViewModel
    {
        public WardrobeItemViewModel(ClothingItem item, byte[]? photoBytes)
        {
            Item = item;
            Name = item.Name;
            Description = $"{GarmentUiMappings.GetLabel(item.Type)} · {item.Color} · {GarmentUiMappings.GetLabel(item.Style)}" +
                (string.IsNullOrWhiteSpace(item.Material) ? string.Empty : $" · {item.Material}");
            Status = $"{GarmentUiMappings.GetLabel(item.Season)} · {(item.IsClean ? "Limpia" : "Para lavar")} · {(item.IsAvailable ? "Disponible" : "No disponible")}";
            Photo = photoBytes is null
                ? null
                : ImageSource.FromStream(() => new MemoryStream(photoBytes, writable: false));
        }

        public ClothingItem Item { get; }
        public string Name { get; }
        public string Description { get; }
        public string Status { get; }
        public ImageSource? Photo { get; }
    }
}
