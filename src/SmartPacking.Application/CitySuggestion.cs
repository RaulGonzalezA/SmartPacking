namespace SmartPacking.Application;

public sealed record CitySuggestion(string Name, string? Country, string? Region, decimal? Latitude = null, decimal? Longitude = null)
{
    public string DisplayName => string.Join(", ", new[] { Name, Region, Country }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
