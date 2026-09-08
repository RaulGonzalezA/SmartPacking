using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SmartPacking.Web;

public sealed class ApiProblemDetailsHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        ValidationProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            // Authentication middleware can return an empty 401 response instead of ProblemDetails.
        }

        var errors = problem?.Errors is { Count: > 0 } validationErrors
            ? new Dictionary<string, string[]>(validationErrors)
            : null;
        var exception = new ApiProblemException((int)response.StatusCode, problem?.Title ?? "No se pudo completar la operación.", problem?.Detail, errors);
        response.Dispose();
        throw exception;
    }
}

public sealed class ApiProblemException(int statusCode, string title, string? detail, IReadOnlyDictionary<string, string[]>? errors = null) : Exception(detail is null ? title : $"{title}: {detail}")
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}
