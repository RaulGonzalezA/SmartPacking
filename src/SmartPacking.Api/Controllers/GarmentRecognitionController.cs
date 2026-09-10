using Microsoft.AspNetCore.Mvc;
using SmartPacking.Application;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/wardrobe/recognition")]
public sealed class GarmentRecognitionController(
    IGarmentRecognizer garmentRecognizer,
    IGarmentRecognitionUsageService recognitionUsage) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(GarmentRecognitionSuggestion), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<GarmentRecognitionSuggestion>> RecognizeAsync(IFormFile photo, CancellationToken cancellationToken)
    {
        if (photo.Length == 0 || photo.Length > 5 * 1024 * 1024 || !string.Equals(photo.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["photo"] = ["Selecciona una foto JPEG de hasta 5 MB."] }));
        }

        var usage = await recognitionUsage.CheckAllowanceAsync(cancellationToken);
        if (!usage.Allowed)
        {
            return Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Límite mensual de reconocimientos alcanzado",
                detail: $"Has utilizado {usage.Used} de {usage.MonthlyLimit} reconocimientos este mes.");
        }

        try
        {
            await using var stream = photo.OpenReadStream();
            var suggestion = await garmentRecognizer.RecognizeAsync(stream, photo.ContentType, cancellationToken);
            var committedUsage = await recognitionUsage.RegisterSuccessfulUsageAsync(cancellationToken);
            if (!committedUsage.Registered)
            {
                return Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Límite mensual de reconocimientos alcanzado",
                    detail: $"Has utilizado {committedUsage.Usage.Used} de {committedUsage.Usage.MonthlyLimit} reconocimientos este mes.");
            }

            return Ok(suggestion);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Reconocimiento no disponible", detail: exception.Message);
        }
        catch (HttpRequestException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Reconocimiento no disponible", detail: "No se ha podido analizar la imagen. Inténtalo de nuevo.");
        }
    }
}
