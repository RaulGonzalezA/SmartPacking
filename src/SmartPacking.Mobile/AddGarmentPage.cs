using System.Globalization;
using SmartPacking.Client;
using SmartPacking.Domain;
using DomainStyle = SmartPacking.Domain.Style;

namespace SmartPacking.Mobile;

public sealed class AddGarmentPage : ContentPage
{
    private readonly ISmartPackingClient client;
    private readonly IMobilePhotoService photoService;
    private readonly Image preview;
    private readonly Label photoInfoLabel;
    private readonly Label statusLabel;
    private readonly Label suitableForLabel;
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
    private readonly Button analyzeButton;
    private readonly Button saveButton;
    private PreparedPhoto? photo;
    private bool busy;

    public AddGarmentPage(ISmartPackingClient client, IMobilePhotoService photoService)
    {
        this.client = client;
        this.photoService = photoService;
        Title = "Añadir prenda";

        preview = new Image { HeightRequest = 240, Aspect = Aspect.AspectFit };
        photoInfoLabel = new Label { Text = "Haz una foto o selecciónala de la galería." };
        statusLabel = new Label();
        suitableForLabel = new Label { FontSize = 13 };

        var cameraButton = new Button { Text = "Hacer foto", IsEnabled = photoService.CanCapturePhoto };
        cameraButton.Clicked += CaptureClicked;
        var galleryButton = new Button { Text = "Galería" };
        galleryButton.Clicked += PickClicked;
        analyzeButton = new Button { Text = "Analizar con Gemini", IsEnabled = false };
        analyzeButton.Clicked += AnalyzeClicked;

        nameEntry = new Entry { Placeholder = "Nombre de la prenda" };
        colorEntry = new Entry { Placeholder = "Color" };
        materialEntry = new Entry { Placeholder = "Material (opcional)" };
        weightEntry = new Entry { Placeholder = "Peso estimado en gramos", Keyboard = Keyboard.Numeric };

        typePicker = CreatePicker("Tipo", GarmentUiMappings.ClothingTypes);
        seasonPicker = CreatePicker("Temporada", GarmentUiMappings.Seasons);
        stylePicker = CreatePicker("Estilo", GarmentUiMappings.Styles);

        warmthSlider = new Slider { Minimum = 1, Maximum = 10, Value = 3 };
        warmthSlider.ValueChanged += WarmthChanged;
        warmthLabel = new Label { Text = "Abrigo: 3/10" };
        waterproofSwitch = new Switch();

        var waterproofRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        var waterproofLabel = new Label { Text = "Impermeable", VerticalOptions = LayoutOptions.Center };
        waterproofRow.Add(waterproofLabel);
        waterproofRow.Add(waterproofSwitch);
        Grid.SetColumn(waterproofSwitch, 1);

        saveButton = new Button { Text = "Guardar en el armario" };
        saveButton.Clicked += SaveClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Nueva prenda", FontSize = 28, FontAttributes = FontAttributes.Bold },
                    preview,
                    photoInfoLabel,
                    new HorizontalStackLayout { Spacing = 10, Children = { cameraButton, galleryButton } },
                    analyzeButton,
                    statusLabel,
                    nameEntry,
                    typePicker,
                    colorEntry,
                    materialEntry,
                    seasonPicker,
                    stylePicker,
                    warmthLabel,
                    warmthSlider,
                    waterproofRow,
                    weightEntry,
                    suitableForLabel,
                    saveButton
                }
            }
        };
    }

    private async void CaptureClicked(object? sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }

        try
        {
            SetBusy(true, "Preparando cámara...");
            var captured = await photoService.CaptureAsync(CancellationToken.None);
            if (captured is not null)
            {
                SetPhoto(captured);
            }
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

        try
        {
            SetBusy(true, "Preparando imagen...");
            var selected = await photoService.PickAsync(CancellationToken.None);
            if (selected is not null)
            {
                SetPhoto(selected);
            }
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

    private async void AnalyzeClicked(object? sender, EventArgs e)
    {
        if (busy || photo is null)
        {
            return;
        }

        try
        {
            SetBusy(true, "Gemini está analizando la prenda...");
            var suggestion = await client.RecognizeGarmentAsync(photo.Content, photo.FileName, CancellationToken.None);
            ApplySuggestion(suggestion);
            statusLabel.Text = "Propuesta aplicada. Revísala antes de guardar.";
        }
        catch (HttpRequestException exception)
        {
            statusLabel.Text = $"No se pudo analizar la prenda: {exception.Message}";
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

        try
        {
            SetBusy(true, "Guardando prenda...");
            var created = await client.CreateClothingItemAsync(request, CancellationToken.None);
            if (photo is not null)
            {
                await client.UploadClothingPhotoAsync(created.Id, photo.Content, photo.FileName, CancellationToken.None);
            }

            statusLabel.Text = "Prenda guardada.";
            await Navigation.PopAsync();
        }
        catch (HttpRequestException exception)
        {
            statusLabel.Text = $"No se pudo guardar la prenda: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetPhoto(PreparedPhoto preparedPhoto)
    {
        photo = preparedPhoto;
        preview.Source = ImageSource.FromStream(() => new MemoryStream(preparedPhoto.Content, writable: false));
        photoInfoLabel.Text = $"Foto optimizada: {preparedPhoto.Width}×{preparedPhoto.Height} · {preparedPhoto.SizeKilobytes} KB";
        analyzeButton.IsEnabled = true;
        statusLabel.Text = "Foto lista para analizar.";
    }

    private void ApplySuggestion(SmartPacking.Application.GarmentRecognitionSuggestion suggestion)
    {
        var type = GarmentUiMappings.ParseCategory(suggestion.Category);
        var season = GarmentUiMappings.ParseSeason(suggestion.Seasons);
        var style = GarmentUiMappings.ParseStyle(suggestion.Style);

        Select(typePicker, GarmentUiMappings.ClothingTypes, type);
        Select(seasonPicker, GarmentUiMappings.Seasons, season);
        Select(stylePicker, GarmentUiMappings.Styles, style);
        colorEntry.Text = suggestion.Color;
        materialEntry.Text = suggestion.Material;
        weightEntry.Text = suggestion.EstimatedWeightGrams.ToString(CultureInfo.InvariantCulture);
        warmthSlider.Value = GarmentUiMappings.EstimateWarmth(type, season);
        if (string.IsNullOrWhiteSpace(nameEntry.Text))
        {
            nameEntry.Text = $"{suggestion.Category} {suggestion.Color}";
        }

        suitableForLabel.Text = suggestion.SuitableFor.Count == 0
            ? string.Empty
            : $"Adecuada para: {string.Join(", ", suggestion.SuitableFor)}";
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
            Material: materialEntry.Text?.Trim());
        validationMessage = string.Empty;
        return true;
    }

    private void WarmthChanged(object? sender, ValueChangedEventArgs e) =>
        warmthLabel.Text = $"Abrigo: {(int)Math.Round(e.NewValue)}/10";

    private void SetBusy(bool value, string? message = null)
    {
        busy = value;
        saveButton.IsEnabled = !value;
        analyzeButton.IsEnabled = !value && photo is not null;
        if (!string.IsNullOrWhiteSpace(message))
        {
            statusLabel.Text = message;
        }
    }

    private static Picker CreatePicker<T>(string title, IReadOnlyList<PickerOption<T>> options)
    {
        var picker = new Picker { Title = title, ItemsSource = options };
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
