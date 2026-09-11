using System.Globalization;
using SmartPacking.Client;
using SmartPacking.Domain;
using DomainStyle = SmartPacking.Domain.Style;

namespace SmartPacking.Mobile;

public sealed class GarmentDetailPage : ContentPage
{
    private readonly ISmartPackingClient client;
    private readonly IMobilePhotoService photoService;
    private readonly PageCancellation pageCancellation = new();
    private readonly Image preview;
    private readonly Label photoInfoLabel;
    private readonly Label statusLabel;
    private readonly Entry nameEntry;
    private readonly Entry colorEntry;
    private readonly Entry materialEntry;
    private readonly Entry weightEntry;
    private readonly Picker typePicker;
    private readonly Picker seasonPicker;
    private readonly Picker stylePicker;
    private readonly Slider warmthSlider;
    private readonly Label warmthLabel;
    private readonly Switch waterproofSwitch;
    private readonly Switch cleanSwitch;
    private readonly Switch availableSwitch;
    private readonly Button captureButton;
    private readonly Button galleryButton;
    private readonly Button saveButton;
    private readonly Button deleteButton;
    private ClothingItem item;
    private PreparedPhoto? pendingPhoto;
    private bool photoLoaded;
    private bool busy;

    public GarmentDetailPage(ISmartPackingClient client, IMobilePhotoService photoService, ClothingItem item)
    {
        this.client = client;
        this.photoService = photoService;
        this.item = item;
        Title = item.Name;

        preview = new Image { HeightRequest = 260, Aspect = Aspect.AspectFit };
        photoInfoLabel = new Label { Text = string.IsNullOrWhiteSpace(item.PhotoUrl) ? "Sin fotografía." : "Cargando fotografía..." };
        statusLabel = new Label();

        nameEntry = new Entry { Text = item.Name, Placeholder = "Nombre" };
        colorEntry = new Entry { Text = item.Color, Placeholder = "Color" };
        materialEntry = new Entry { Text = item.Material, Placeholder = "Material (opcional)" };
        weightEntry = new Entry
        {
            Text = item.WeightGrams?.ToString(CultureInfo.InvariantCulture),
            Placeholder = "Peso estimado en gramos",
            Keyboard = Keyboard.Numeric
        };

        typePicker = CreatePicker("Tipo", GarmentUiMappings.ClothingTypes);
        seasonPicker = CreatePicker("Temporada", GarmentUiMappings.Seasons);
        stylePicker = CreatePicker("Estilo", GarmentUiMappings.Styles);
        Select(typePicker, GarmentUiMappings.ClothingTypes, item.Type);
        Select(seasonPicker, GarmentUiMappings.Seasons, item.Season);
        Select(stylePicker, GarmentUiMappings.Styles, item.Style);

        warmthSlider = new Slider { Minimum = 1, Maximum = 10, Value = item.WarmthLevel };
        warmthSlider.ValueChanged += WarmthChanged;
        warmthLabel = new Label { Text = $"Abrigo: {item.WarmthLevel}/10" };
        waterproofSwitch = new Switch { IsToggled = item.Waterproof };
        cleanSwitch = new Switch { IsToggled = item.IsClean };
        availableSwitch = new Switch { IsToggled = item.IsAvailable };

        captureButton = new Button { Text = "Nueva foto", IsEnabled = photoService.CanCapturePhoto };
        captureButton.Clicked += CaptureClicked;
        galleryButton = new Button { Text = "Galería" };
        galleryButton.Clicked += PickClicked;
        saveButton = new Button { Text = "Guardar cambios" };
        saveButton.Clicked += SaveClicked;
        deleteButton = new Button { Text = "Eliminar prenda" };
        deleteButton.Clicked += DeleteClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label { Text = item.Name, FontSize = 28, FontAttributes = FontAttributes.Bold },
                    preview,
                    photoInfoLabel,
                    new HorizontalStackLayout { Spacing = 10, Children = { captureButton, galleryButton } },
                    statusLabel,
                    nameEntry,
                    typePicker,
                    colorEntry,
                    materialEntry,
                    seasonPicker,
                    stylePicker,
                    warmthLabel,
                    warmthSlider,
                    CreateSwitchRow("Impermeable", waterproofSwitch),
                    CreateSwitchRow("Limpia", cleanSwitch),
                    CreateSwitchRow("Disponible", availableSwitch),
                    weightEntry,
                    saveButton,
                    deleteButton
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        pageCancellation.Reset();
        if (!photoLoaded && !string.IsNullOrWhiteSpace(item.PhotoUrl))
        {
            await LoadPhotoAsync(pageCancellation.Token);
        }
    }

    protected override void OnDisappearing()
    {
        pageCancellation.Release();
        base.OnDisappearing();
    }

    private async Task LoadPhotoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await client.GetClothingPhotoAsync(item.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            photoLoaded = true;
            if (bytes is null)
            {
                photoInfoLabel.Text = "Sin fotografía.";
                return;
            }

            preview.Source = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
            photoInfoLabel.Text = "Fotografía actual.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            statusLabel.Text = "Carga de fotografía cancelada.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            photoLoaded = true;
            photoInfoLabel.Text = "No se pudo cargar la fotografía.";
            statusLabel.Text = exception.Message;
        }
    }

    private async void CaptureClicked(object? sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }

        var cancellationToken = pageCancellation.Token;
        try
        {
            SetBusy(true, "Preparando cámara...");
            var photo = await photoService.CaptureAsync(cancellationToken);
            if (photo is not null)
            {
                SetPhoto(photo);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            statusLabel.Text = "Captura cancelada.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or FeatureNotSupportedException or PermissionException)
        {
            statusLabel.Text = $"No se pudo abrir la cámara: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void PickClicked(object? sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }

        var cancellationToken = pageCancellation.Token;
        try
        {
            SetBusy(true, "Preparando imagen...");
            var photo = await photoService.PickAsync(cancellationToken);
            if (photo is not null)
            {
                SetPhoto(photo);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            statusLabel.Text = "Selección cancelada.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or FeatureNotSupportedException or PermissionException)
        {
            statusLabel.Text = $"No se pudo abrir la galería: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void SaveClicked(object? sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }

        if (!TryBuildRequest(out var request, out var validationMessage))
        {
            statusLabel.Text = validationMessage;
            return;
        }

        var cancellationToken = pageCancellation.Token;
        try
        {
            SetBusy(true, "Guardando cambios...");
            item = await client.UpdateClothingItemAsync(item.Id, request, cancellationToken);
            Title = item.Name;

            if (pendingPhoto is not null)
            {
                statusLabel.Text = "Subiendo fotografía...";
                await client.UploadClothingPhotoAsync(
                    item.Id,
                    pendingPhoto.Content,
                    pendingPhoto.FileName,
                    cancellationToken,
                    pendingPhoto.ThumbnailContent);
                pendingPhoto = null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            statusLabel.Text = "Cambios guardados.";
            await Navigation.PopAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            statusLabel.Text = "Guardado cancelado.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            statusLabel.Text = pendingPhoto is null
                ? $"No se pudieron guardar los cambios: {exception.Message}"
                : "Los datos de la prenda pueden haberse guardado, pero la fotografía sigue pendiente. Vuelve a pulsar Guardar para reintentar.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void DeleteClicked(object? sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync(
            "Eliminar prenda",
            $"¿Quieres enviar '{item.Name}' a la papelera?",
            "Eliminar",
            "Cancelar");
        if (!confirmed)
        {
            return;
        }

        var cancellationToken = pageCancellation.Token;
        try
        {
            SetBusy(true, "Eliminando prenda...");
            await client.DeleteClothingItemAsync(item.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await Navigation.PopAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            statusLabel.Text = "Eliminación cancelada.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            statusLabel.Text = $"No se pudo eliminar la prenda: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetPhoto(PreparedPhoto photo)
    {
        pendingPhoto = photo;
        preview.Source = ImageSource.FromStream(() => new MemoryStream(photo.Content, writable: false));
        photoInfoLabel.Text = $"Nueva foto: {photo.Width}×{photo.Height} · {photo.SizeKilobytes} KB · miniatura {photo.ThumbnailSizeKilobytes} KB";
        statusLabel.Text = "La nueva fotografía se subirá al guardar.";
    }

    private bool TryBuildRequest(out CreateClothingItemRequest request, out string validationMessage)
    {
        var name = nameEntry.Text?.Trim() ?? string.Empty;
        var color = colorEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            request = default!;
            validationMessage = "Indica un nombre para la prenda.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(color))
        {
            request = default!;
            validationMessage = "Indica el color de la prenda.";
            return false;
        }

        int? weight = null;
        if (!string.IsNullOrWhiteSpace(weightEntry.Text))
        {
            if (!int.TryParse(weightEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWeight) || parsedWeight is < 1 or > 10000)
            {
                request = default!;
                validationMessage = "El peso debe ser un número entre 1 y 10000 gramos.";
                return false;
            }

            weight = parsedWeight;
        }

        request = new CreateClothingItemRequest(
            name,
            Selected(typePicker, ClothingType.Accessory),
            Selected(seasonPicker, Season.AllYear),
            color,
            (int)Math.Round(warmthSlider.Value),
            waterproofSwitch.IsToggled,
            Selected(stylePicker, DomainStyle.Casual),
            weight,
            cleanSwitch.IsToggled,
            availableSwitch.IsToggled,
            item.PreferenceScore,
            item.OwnerProfileId,
            materialEntry.Text?.Trim(),
            item.CombinesWith);
        validationMessage = string.Empty;
        return true;
    }

    private void WarmthChanged(object? sender, ValueChangedEventArgs e) =>
        warmthLabel.Text = $"Abrigo: {(int)Math.Round(e.NewValue)}/10";

    private void SetBusy(bool value, string? message = null)
    {
        busy = value;
        saveButton.IsEnabled = !value;
        deleteButton.IsEnabled = !value;
        captureButton.IsEnabled = !value && photoService.CanCapturePhoto;
        galleryButton.IsEnabled = !value;
        if (!string.IsNullOrWhiteSpace(message))
        {
            statusLabel.Text = message;
        }
    }

    private static Grid CreateSwitchRow(string text, Switch toggle)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        row.Add(new Label { Text = text, VerticalOptions = LayoutOptions.Center });
        row.Add(toggle);
        Grid.SetColumn(toggle, 1);
        return row;
    }

    private static Picker CreatePicker<T>(string title, IReadOnlyList<PickerOption<T>> options)
    {
        var picker = new Picker { Title = title, ItemsSource = options.ToArray() };
        picker.SelectedIndex = 0;
        return picker;
    }

    private static T Selected<T>(Picker picker, T fallback) =>
        picker.SelectedItem is PickerOption<T> option ? option.Value : fallback;

    private static void Select<T>(Picker picker, IReadOnlyList<PickerOption<T>> options, T value)
        where T : struct, Enum
    {
        var index = options.Select((option, position) => (option, position))
            .Where(entry => EqualityComparer<T>.Default.Equals(entry.option.Value, value))
            .Select(entry => entry.position)
            .DefaultIfEmpty(0)
            .First();
        picker.SelectedIndex = index;
    }
}
