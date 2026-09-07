using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SmartPacking.Api.Validation;

public static class ValidationExtensions
{
    public static async Task<ValidationProblemDetails?> ToProblemDetailsAsync<T>(this IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
        {
            return null;
        }

        return new ValidationProblemDetails(result.Errors
            .GroupBy(error => ToCamelCase(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()));
    }

    private static string ToCamelCase(string value) => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
