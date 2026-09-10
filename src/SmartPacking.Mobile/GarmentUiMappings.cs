using SmartPacking.Domain;
using DomainStyle = SmartPacking.Domain.Style;

namespace SmartPacking.Mobile;

internal sealed record PickerOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

internal static class GarmentUiMappings
{
    public static IReadOnlyList<PickerOption<ClothingType>> ClothingTypes { get; } =
        Enum.GetValues<ClothingType>().Select(value => new PickerOption<ClothingType>(value, GetLabel(value))).ToArray();

    public static IReadOnlyList<PickerOption<Season>> Seasons { get; } =
        Enum.GetValues<Season>().Select(value => new PickerOption<Season>(value, GetLabel(value))).ToArray();

    public static IReadOnlyList<PickerOption<DomainStyle>> Styles { get; } =
        Enum.GetValues<DomainStyle>().Select(value => new PickerOption<DomainStyle>(value, GetLabel(value))).ToArray();

    public static ClothingType ParseCategory(string category) => category.Trim() switch
    {
        "Camiseta" => ClothingType.TShirt,
        "Camisa" => ClothingType.Shirt,
        "Jersey" => ClothingType.Sweater,
        "Sudadera" => ClothingType.Hoodie,
        "Abrigo" => ClothingType.Coat,
        "Chaqueta" => ClothingType.Jacket,
        "Vestido" => ClothingType.Dress,
        "Falda" => ClothingType.Skirt,
        "Pantalón" => ClothingType.Trousers,
        "Pantalón corto" => ClothingType.Shorts,
        "Ropa interior" => ClothingType.Underwear,
        "Calcetines" => ClothingType.Socks,
        "Bañador" => ClothingType.Swimwear,
        "Pijama" => ClothingType.Pyjamas,
        "Cinturón" => ClothingType.Belt,
        "Bolso" => ClothingType.Bag,
        "Zapatos" => ClothingType.Shoes,
        "Sandalias" => ClothingType.Sandals,
        _ => ClothingType.Accessory
    };

    public static Season ParseSeason(IReadOnlyCollection<string> seasons)
    {
        var summer = seasons.Contains("Verano", StringComparer.OrdinalIgnoreCase);
        var winter = seasons.Contains("Invierno", StringComparer.OrdinalIgnoreCase);
        var midSeason = seasons.Contains("Primavera", StringComparer.OrdinalIgnoreCase) ||
                        seasons.Contains("Otoño", StringComparer.OrdinalIgnoreCase);

        if (summer && !winter && !midSeason)
        {
            return Season.Summer;
        }

        if (winter && !summer && !midSeason)
        {
            return Season.Winter;
        }

        return midSeason && !summer && !winter ? Season.MidSeason : Season.AllYear;
    }

    public static DomainStyle ParseStyle(string style) => style.Trim() switch
    {
        "Formal" => DomainStyle.Formal,
        "Deportivo" => DomainStyle.Sport,
        "Negocios" => DomainStyle.Business,
        _ => DomainStyle.Casual
    };

    public static int EstimateWarmth(ClothingType type, Season season) => type switch
    {
        ClothingType.Coat => 9,
        ClothingType.Jacket => 7,
        ClothingType.Sweater => 7,
        ClothingType.Hoodie => 6,
        ClothingType.Shirt => season == Season.Winter ? 5 : 4,
        ClothingType.Trousers => 5,
        ClothingType.Dress => 4,
        ClothingType.Pyjamas => 4,
        ClothingType.TShirt => 3,
        ClothingType.Shoes => 3,
        ClothingType.Skirt => 3,
        ClothingType.Shorts => 2,
        ClothingType.Sandals => 2,
        ClothingType.Swimwear => 1,
        _ => 3
    };

    public static string GetLabel(ClothingType type) => type switch
    {
        ClothingType.TShirt => "Camiseta",
        ClothingType.Trousers => "Pantalón",
        ClothingType.Shorts => "Pantalón corto",
        ClothingType.Jacket => "Chaqueta",
        ClothingType.Shoes => "Zapatos",
        ClothingType.Sandals => "Sandalias",
        ClothingType.Accessory => "Accesorio",
        ClothingType.Shirt => "Camisa",
        ClothingType.Sweater => "Jersey",
        ClothingType.Hoodie => "Sudadera",
        ClothingType.Coat => "Abrigo",
        ClothingType.Dress => "Vestido",
        ClothingType.Skirt => "Falda",
        ClothingType.Underwear => "Ropa interior",
        ClothingType.Socks => "Calcetines",
        ClothingType.Swimwear => "Bañador",
        ClothingType.Pyjamas => "Pijama",
        ClothingType.Belt => "Cinturón",
        ClothingType.Bag => "Bolso",
        _ => type.ToString()
    };

    public static string GetLabel(Season season) => season switch
    {
        Season.Summer => "Verano",
        Season.Winter => "Invierno",
        Season.MidSeason => "Entretiempo",
        Season.AllYear => "Todo el año",
        _ => season.ToString()
    };

    public static string GetLabel(DomainStyle style) => style switch
    {
        DomainStyle.Casual => "Casual",
        DomainStyle.Formal => "Formal",
        DomainStyle.Sport => "Deportivo",
        DomainStyle.Business => "Negocios",
        _ => style.ToString()
    };
}
