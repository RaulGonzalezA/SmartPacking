using System.Net.Http.Json;
using System.Text.Json;
using SmartPacking.Application;

namespace SmartPacking.Api;

public interface IGarmentRecognizer
{
    Task<GarmentRecognitionSuggestion> RecognizeAsync(Stream photo, string contentType, CancellationToken cancellationToken);
}

public sealed partial class GeminiGarmentRecognizer(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiGarmentRecognizer> logger) : IGarmentRecognizer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Prompt = "Analiza exclusivamente la prenda principal de la imagen. Responde en español y solo con JSON. category debe ser uno de Camiseta, Camisa, Jersey, Sudadera, Abrigo, Chaqueta, Vestido, Falda, Pantalón, Pantalón corto, Ropa interior, Calcetines, Bañador, Pijama, Cinturón, Bolso, Zapatos, Sandalias o Accesorio. season contiene Primavera, Verano, Otoño o Invierno; style uno de Casual, Formal, Deportivo o Negocios. estimatedWeightGrams es una estimación entera razonable. suitableFor contiene Turismo, Ocio, Playa, Senderismo o Negocios.";

    public async Task<GarmentRecognitionSuggestion> RecognizeAsync(Stream photo, string contentType, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("El reconocimiento de prendas no está configurado.");
        }

        using var memory = new MemoryStream();
        await photo.CopyToAsync(memory, cancellationToken);
        var request = new
        {
            model = configuration["Gemini:Model"] ?? "gemini-3.5-flash-lite",
            input = new object[] { new { type = "text", text = Prompt }, new { type = "image", mime_type = contentType, data = Convert.ToBase64String(memory.ToArray()) } },
            response_format = new { type = "text", mime_type = "application/json" },
            store = false
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1beta/interactions") { Content = JsonContent.Create(request) };
        message.Headers.Add("x-goog-api-key", apiKey);
        message.Headers.Add("Api-Revision", "2026-05-20");
        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            LogGeminiFailure(logger, response.StatusCode, detail);
            throw new HttpRequestException($"Gemini no pudo analizar la imagen: {detail}", null, response.StatusCode);
        }

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var text = document.RootElement.GetProperty("steps").EnumerateArray()
            .Where(step => step.GetProperty("type").GetString() == "model_output")
            .SelectMany(step => step.GetProperty("content").EnumerateArray())
            .First(part => part.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString();
        var suggestion = JsonSerializer.Deserialize<GarmentRecognitionSuggestion>(text ?? string.Empty, JsonOptions);
        return suggestion ?? throw new InvalidOperationException("Gemini no devolvió una propuesta válida.");
    }

    [LoggerMessage(LogLevel.Warning, "Gemini rechazó el reconocimiento con estado {StatusCode}. Detalle: {Detail}")]
    private static partial void LogGeminiFailure(ILogger logger, System.Net.HttpStatusCode statusCode, string detail);
}
