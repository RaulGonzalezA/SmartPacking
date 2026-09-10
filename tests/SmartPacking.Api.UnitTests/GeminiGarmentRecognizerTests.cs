using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPacking.Api;
using Xunit;

namespace SmartPacking.Api.UnitTests;

public sealed class GeminiGarmentRecognizerTests
{
    [Fact]
    public async Task RecognizeAsyncPreservesTheBusinessStyleReturnedByGemini()
    {
        const string response = """
            {"steps":[{"type":"model_output","content":[{"type":"text","text":"{\"category\":\"Camisa\",\"color\":\"Azul\",\"material\":\"Algodón\",\"seasons\":[\"Invierno\"],\"style\":\"Negocios\",\"estimatedWeightGrams\":250,\"suitableFor\":[\"Negocios\"]}"}]}]}
            """;
        using var client = new HttpClient(new StaticResponseHandler(response)) { BaseAddress = new Uri("https://gemini.example/") };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Gemini:ApiKey"] = "test-key" })
            .Build();
        var recognizer = new GeminiGarmentRecognizer(client, configuration, NullLogger<GeminiGarmentRecognizer>.Instance);
        await using var photo = new MemoryStream([0xFF, 0xD8, 0xFF, 0xD9]);

        var suggestion = await recognizer.RecognizeAsync(photo, "image/jpeg", CancellationToken.None);

        suggestion.Style.Should().Be("Negocios");
    }

    private sealed class StaticResponseHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
    }
}
