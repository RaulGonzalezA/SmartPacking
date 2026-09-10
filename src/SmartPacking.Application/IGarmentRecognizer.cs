namespace SmartPacking.Application;

public interface IGarmentRecognizer
{
    Task<GarmentRecognitionSuggestion> RecognizeAsync(Stream photo, string contentType, CancellationToken cancellationToken);
}
