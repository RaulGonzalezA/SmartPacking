using System.Net;

namespace SmartPacking.Web;

public enum ApiOperationStatus
{
    Success,
    ValidationError,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    TransientFailure,
    UnexpectedFailure
}

/// <summary>UI-safe classification of a failed API operation.</summary>
public sealed record ApiOperationResult(
    ApiOperationStatus Status,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public bool IsSuccess => Status == ApiOperationStatus.Success;
    public IReadOnlyDictionary<string, string[]> Errors { get; } = ValidationErrors ?? new Dictionary<string, string[]>();

    public static ApiOperationResult Success() => new(ApiOperationStatus.Success);

    public static ApiOperationResult FromException(Exception exception) => exception switch
    {
        ApiProblemException { StatusCode: StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity } problem when problem.Errors.Count > 0 =>
            new(ApiOperationStatus.ValidationError, problem.Detail ?? problem.Title, problem.Errors),
        ApiProblemException { StatusCode: StatusCodes.Status401Unauthorized } =>
            new(ApiOperationStatus.Unauthorized, "Tu sesión ha caducado. Inicia sesión de nuevo."),
        ApiProblemException { StatusCode: StatusCodes.Status403Forbidden } =>
            new(ApiOperationStatus.Forbidden, "No tienes permisos para realizar esta acción."),
        ApiProblemException { StatusCode: StatusCodes.Status404NotFound } problem =>
            new(ApiOperationStatus.NotFound, problem.Detail ?? problem.Title),
        ApiProblemException { StatusCode: StatusCodes.Status409Conflict } problem =>
            new(ApiOperationStatus.Conflict, problem.Detail ?? problem.Title),
        ApiProblemException { StatusCode: >= StatusCodes.Status500InternalServerError } =>
            new(ApiOperationStatus.TransientFailure, "El servicio no está disponible en este momento. Inténtalo de nuevo."),
        ApiProblemException problem => new(ApiOperationStatus.UnexpectedFailure, problem.Detail ?? problem.Title),
        HttpRequestException => new(ApiOperationStatus.TransientFailure, "No se ha podido conectar con el servicio. Inténtalo de nuevo."),
        _ => new(ApiOperationStatus.UnexpectedFailure, "Ha ocurrido un error inesperado. Inténtalo de nuevo.")
    };

    public static async Task<ApiOperationResult> ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return FromException(exception);
        }
    }
}
