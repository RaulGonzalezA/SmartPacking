using Microsoft.Extensions.DependencyInjection;
using SmartPacking.Client;
using SmartPacking.Domain;

namespace SmartPacking.Mobile;

public sealed class WardrobePage : ContentPage
{
    private readonly ISmartPackingClient client;
    private readonly IServiceProvider services;
    private readonly CollectionView wardrobeView;
    private readonly Label statusLabel;
    private bool loading;

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
            SelectionMode = SelectionMode.None,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 },
            EmptyView = new Label { Text = "Todavía no tienes prendas en el armario.", Margin = new Thickness(0, 24) },
            ItemTemplate = new DataTemplate(CreateGarmentCard)
        };

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
        await LoadAsync();
    }

    private async void AddGarmentClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(services.GetRequiredService<AddGarmentPage>());

    private async void RefreshClicked(object? sender, EventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        statusLabel.Text = "Cargando armario...";
        try
        {
            var wardrobe = await client.GetWardrobeAsync(CancellationToken.None);
            using var gate = new SemaphoreSlim(4);
            var models = await Task.WhenAll(wardrobe
                .OrderBy(item => item.Type)
                .ThenBy(item => item.Name)
                .Select(item => CreateViewModelAsync(item, gate, CancellationToken.None)));
            wardrobeView.ItemsSource = models;
            statusLabel.Text = $"{models.Length} prendas";
        }
        catch (HttpRequestException exception)
        {
            wardrobeView.ItemsSource = null;
            statusLabel.Text = $"No se pudo cargar el armario: {exception.Message}";
        }
        finally
        {
            loading = false;
        }
    }

    private async Task<WardrobeItemViewModel> CreateViewModelAsync(ClothingItem item, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        byte[]? photoBytes = null;
        if (!string.IsNullOrWhiteSpace(item.PhotoUrl))
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                photoBytes = await client.GetClothingPhotoAsync(item.Id, cancellationToken);
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
            HeightRequest = 120,
            WidthRequest = 120,
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
                new ColumnDefinition { Width = 130 },
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
        private readonly byte[]? photoBytes;

        public WardrobeItemViewModel(ClothingItem item, byte[]? photoBytes)
        {
            this.photoBytes = photoBytes;
            Name = item.Name;
            Description = $"{GarmentUiMappings.GetLabel(item.Type)} · {item.Color} · {GarmentUiMappings.GetLabel(item.Style)}" +
                (string.IsNullOrWhiteSpace(item.Material) ? string.Empty : $" · {item.Material}");
            Status = $"{GarmentUiMappings.GetLabel(item.Season)} · {(item.IsClean ? "Limpia" : "Para lavar")} · {(item.IsAvailable ? "Disponible" : "No disponible")}";
        }

        public string Name { get; }
        public string Description { get; }
        public string Status { get; }
        public ImageSource? Photo => photoBytes is null ? null : ImageSource.FromStream(() => new MemoryStream(photoBytes, writable: false));
    }
}
