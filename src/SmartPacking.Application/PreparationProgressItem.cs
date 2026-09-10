namespace SmartPacking.Application;

public sealed record PreparationProgressItem(string Name, int PackedClothing, int TotalClothing, int PackedChecklist, int TotalChecklist)
{
    public int Percent => TotalClothing + TotalChecklist == 0
        ? 0
        : (int)Math.Round((PackedClothing + PackedChecklist) * 100d / (TotalClothing + TotalChecklist));
}
