namespace SmartPacking.Web.Components;

public sealed record PreparationProgressItem(string Name, int PackedClothing, int TotalClothing, int PackedChecklist, int TotalChecklist)
{
    public int Percent { get { var total = TotalClothing + TotalChecklist; return total == 0 ? 100 : (int)Math.Round((PackedClothing + PackedChecklist) * 100m / total); } }
}
