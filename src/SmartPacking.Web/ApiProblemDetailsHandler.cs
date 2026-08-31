using System.Net.Http.Json;
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

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
        var exception = new ApiProblemException((int)response.StatusCode, problem?.Title ?? "No se pudo completar la operación.", problem?.Detail);
        response.Dispose();
        throw exception;
    }
}

public sealed class ApiProblemException(int statusCode, string title, string? detail) : Exception(detail is null ? title : $"{title}: {detail}")
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
}
