namespace SmartPacking.Application;

public sealed record ToiletryBagItem(string Name, int EstimatedWeightGrams, string Category);

public static class ToiletryBagPlan
{
    public static readonly IReadOnlyList<ToiletryBagItem> Essentials =
    [
        new("Cepillo y pasta de dientes", 120, "Higiene"),
        new("Desodorante", 90, "Higiene"),
        new("Champú o gel en formato viaje", 120, "Higiene"),
        new("Protector solar", 140, "Cuidado"),
        new("Crema hidratante", 80, "Cuidado"),
        new("Peine y básicos", 70, "Accesorios")
    ];

    public static int EstimatedWeightGrams => Essentials.Sum(item => item.EstimatedWeightGrams);
}
