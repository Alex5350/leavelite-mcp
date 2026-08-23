using System.Globalization;
using ErrorOr;
using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Employees;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Server.Tools;

/// <summary>
/// Shared plumbing for the MCP tools: employee resolution by email-or-Guid, friendly parsing of
/// dates/months/leave types, and ErrorOr-to-readable-text mapping (always including the stable
/// error code so callers can assert on it).
/// </summary>
internal static class ToolHelpers
{
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>Formats ErrorOr failures as readable text with the machine-stable error code first.</summary>
    public static string Failure(IReadOnlyList<Error> errors)
        => $"Request failed with {errors.Count} error(s). "
            + "Each error is listed as [stable.error.code] human-readable description:\n"
            + string.Join("\n", errors.Select(error => $"- [{error.Code}] {error.Description}"));

    /// <summary>
    /// Resolves an employee by work email (friendliest for AI callers) or by Guid id.
    /// Falls through to Guid parsing when the input is not a valid email address.
    /// </summary>
    public static async Task<Employee?> FindEmployeeAsync(IEmployeeRepository employees, string emailOrId, CancellationToken cancellationToken)
    {
        if (Email.TryCreate(emailOrId, out var email))
        {
            var byEmail = await employees.GetByEmailAsync(email, cancellationToken);
            if (byEmail is not null)
            {
                return byEmail;
            }
        }

        if (Guid.TryParse(emailOrId, out var id))
        {
            return await employees.GetByIdAsync(new EmployeeId(id), cancellationToken);
        }

        return null;
    }

    public static string UnknownEmployee(string emailOrId)
        => $"No employee matches '{emailOrId}'. Provide a work email (e.g. ada@leavelite.io) or a Guid employee id. "
            + "Call list_employees to see the directory.";

    public static bool TryParseDate(string? text, out DateOnly date)
        => DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static string BadDate(string parameterName, string? value)
        => $"'{parameterName}' must be a calendar date in ISO format {DateFormat} (got '{value}').";

    /// <summary>Parses a month given as "yyyy-MM" into the first day of that month.</summary>
    public static bool TryParseMonth(string? text, out DateOnly firstDayOfMonth)
        => DateOnly.TryParseExact(
            string.Create(CultureInfo.InvariantCulture, $"{text}-01"),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out firstDayOfMonth);

    public static bool TryParseLeaveType(string? text, out LeaveType leaveType)
        => Enum.TryParse(text, ignoreCase: true, out leaveType) && Enum.IsDefined(leaveType);

    public static string BadLeaveType(string? value)
        => $"'leaveType' must be one of Vacation, Sick or Parental (got '{value}').";

    public static bool TryParseRequestId(string? text, out LeaveRequestId requestId)
    {
        if (Guid.TryParse(text, out var guid))
        {
            requestId = new LeaveRequestId(guid);
            return true;
        }

        requestId = default;
        return false;
    }

    public static string Hours(decimal hours)
        => hours.ToString("0.##", CultureInfo.InvariantCulture) + "h";
}
