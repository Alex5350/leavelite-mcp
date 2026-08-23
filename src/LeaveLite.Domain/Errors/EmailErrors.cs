using ErrorOr;

namespace LeaveLite.Domain.Errors;

public static class EmailErrors
{
    public static Error Invalid(string? input)
        => Error.Validation("Email.Invalid", $"'{input ?? "<null>"}' is not a valid email address.");
}
