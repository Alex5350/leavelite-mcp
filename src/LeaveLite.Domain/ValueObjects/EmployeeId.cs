namespace LeaveLite.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for the <see cref="LeaveLite.Domain.Employees.Employee"/> aggregate.
/// Wraps a GUID; <see cref="New"/> issues a sortable Guid v7 so insertion order is preserved.
/// </summary>
public readonly record struct EmployeeId(Guid Value)
{
    public static EmployeeId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
