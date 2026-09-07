namespace SmartPacking.Application;

public sealed record GarmentRecognitionSuggestion(string Category, string Color, string? Material, IReadOnlyCollection<string> Seasons, string Style, int EstimatedWeightGrams, IReadOnlyCollection<string> SuitableFor);
