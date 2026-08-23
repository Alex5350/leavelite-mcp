using LeaveLite.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LeaveLite.Infrastructure.Persistence;

/// <summary>
/// EF Core value converters for the domain's strongly-typed ids and value objects.
/// Typed ids round-trip through Guid; <see cref="Email"/> through its normalized string form.
/// </summary>
internal static class PersistenceConverters
{
    public static readonly ValueConverter<EmployeeId, Guid> EmployeeIdToGuid =
        new(id => id.Value, value => new EmployeeId(value));

    public static readonly ValueConverter<AccrualPolicyId, Guid> AccrualPolicyIdToGuid =
        new(id => id.Value, value => new AccrualPolicyId(value));

    public static readonly ValueConverter<LeaveRequestId, Guid> LeaveRequestIdToGuid =
        new(id => id.Value, value => new LeaveRequestId(value));

    public static readonly ValueConverter<Email, string> EmailToString =
        new(email => email.Value, value => FromStorage(value));

    /// <summary>
    /// Rehydrates an <see cref="Email"/> persisted in the database. Stored values were validated
    /// on write, so an invalid value here means data corruption — fail loudly instead of guessing.
    /// </summary>
    private static Email FromStorage(string value)
        => Email.TryCreate(value, out var email)
            ? email
            : throw new InvalidOperationException($"Stored email '{value}' is not a valid Email value object.");
}
