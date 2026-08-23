using ErrorOr;
using FluentValidation;
using FluentValidation.Results;

namespace LeaveLite.Application.Common;

/// <summary>FluentValidation-to-ErrorOr plumbing shared by all handlers.</summary>
public static class ValidationMappings
{
    /// <summary>
    /// Validates and returns the mapped errors, or <c>null</c> when the instance is valid.
    /// Handlers do: <c>if (await validator.ValidateToErrorsAsync(cmd, ct) is { } errors) return errors;</c>
    /// </summary>
    public static async Task<List<Error>?> ValidateToErrorsAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        ValidationResult result = await validator.ValidateAsync(instance, cancellationToken);
        return result.IsValid ? null : ToErrors(result.Errors);
    }

    public static List<Error> ToErrors(this IEnumerable<ValidationFailure> failures)
        => [.. failures.Select(static failure => Error.Validation(failure.PropertyName, failure.ErrorMessage))];
}
