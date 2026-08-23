using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ErrorOr;
using LeaveLite.Domain.Errors;

namespace LeaveLite.Domain.ValueObjects;

/// <summary>
/// A normalized email address value object. Stored trimmed and lower-cased.
/// </summary>
public sealed partial record Email
{
    private Email(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? input, [NotNullWhen(true)] out Email? email)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            email = null;
            return false;
        }

        var candidate = input.Trim().ToLowerInvariant();
        if (candidate.Length > 254 || !Pattern().IsMatch(candidate))
        {
            email = null;
            return false;
        }

        email = new Email(candidate);
        return true;
    }

    /// <summary>Creates a validated email or returns <see cref="EmailErrors.Invalid"/>.</summary>
    public static ErrorOr<Email> Create(string? input)
        => TryCreate(input, out var email) ? email : EmailErrors.Invalid(input);

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Pattern();
}
