using Microsoft.AspNetCore.Mvc;
using SmartPacking.Application;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/wardrobe/recognition")]
public sealed class GarmentRecognitionController(IGarmentRecognizer garmentRecognizer) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(GarmentRecognitionSuggestion), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GarmentRecognitionSuggestion>> RecognizeAsync(IFormFile photo, CancellationToken cancellationToken)
    {
        if (photo.Length == 0 || photo.Length > 5 * 1024 * 1024 || !string.Equals(photo.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["photo"] = ["Selecciona una foto JPEG de hasta 5 MB."] }));
        }
        try 
        { 
            await using var stream = photo.OpenReadStream(); 
            return Ok(await garmentRecognizer.RecognizeAsync(stream, photo.ContentType, cancellationToken)); 
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
