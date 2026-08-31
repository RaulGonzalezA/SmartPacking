using SmartPacking.Domain;

namespace SmartPacking.Application;

public static class DemoData
{
    public static Trip RomeTrip { get; } = new(Guid.Parse("d842d38d-9f57-43bd-bc05-4af07328f507"), "Roma", new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 14), 24, 34, [Style.Casual, Style.Formal]);

    public static IReadOnlyList<ClothingItem> Wardrobe { get; } = CreateWardrobe();

    private static IReadOnlyList<ClothingItem> CreateWardrobe()
    {
        var whiteTee = Guid.Parse("90e5486c-49ce-41dc-9190-b5c32625c021");
        var beigeTrousers = Guid.Parse("9a105982-3841-405f-8c6c-d04f06bfa3f9");
        var sneakers = Guid.Parse("82050e90-bf4c-4783-991c-4e4b456095b7");
        return
        [
            new(whiteTee, "Camiseta blanca Uniqlo", ClothingType.TShirt, Season.Summer, "Blanco", 1, false, Style.Casual, 140, true, true, 95, [beigeTrousers, sneakers]),
            new(Guid.Parse("6d0e9d2c-f28c-4a2e-a331-54be1ef75f6b"), "Camiseta azul Nike", ClothingType.TShirt, Season.Summer, "Azul", 1, false, Style.Sport, 130, true, true, 82, [sneakers]),
            new(Guid.Parse("f3f4e90c-4b66-4057-a8ad-fcdb6e85bb35"), "Camiseta gris Springfield", ClothingType.TShirt, Season.Summer, "Gris", 2, false, Style.Casual, 160, true, true, 78, [beigeTrousers]),
            new(beigeTrousers, "Pantalón beige", ClothingType.Trousers, Season.AllYear, "Beige", 3, false, Style.Casual, 420, true, true, 90, [whiteTee]),
            new(Guid.Parse("2402c053-8ff4-4603-84f6-322d4c0d5eb1"), "Pantalón corto negro", ClothingType.Shorts, Season.Summer, "Negro", 1, false, Style.Casual, 220, true, true, 85, [whiteTee, sneakers]),
            new(sneakers, "Zapatillas New Balance", ClothingType.Shoes, Season.AllYear, "Blanco", 3, false, Style.Casual, 700, true, true, 92, [whiteTee, beigeTrousers]),
            new(Guid.Parse("e114db34-b7a3-43a7-8b91-a1d8c5f32a36"), "Sandalias marrones", ClothingType.Sandals, Season.Summer, "Marrón", 1, false, Style.Casual, 420, true, true, 75, [beigeTrousers]),
            new(Guid.Parse("149dc43f-4c42-4ee8-b1ec-1ec28803ce42"), "Chaqueta impermeable", ClothingType.Jacket, Season.MidSeason, "Azul", 7, true, Style.Casual, 650, true, true, 70, [])
        ];
    }
}
