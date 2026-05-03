using System.Globalization;
using System.Text.RegularExpressions;

namespace Appraisal.Domain.Projects;

/// <summary>
/// Validates a partial-precision date string used by Project.ProjectSaleLaunchDate.
/// Three accepted shapes:
///   • "YYYY"        — year only
///   • "YYYY-MM"     — year + month
///   • "YYYY-MM-DD"  — full calendar date (rejects invalid days like 2025-02-30)
/// Null / empty is treated as "no value" and is considered valid (caller decides
/// whether the field is optional).
/// </summary>
public static partial class PartialDate
{
    private static readonly Regex Shape = MyRegex();

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!Shape.IsMatch(value)) return false;

        // Full-date case: also verify it's a real calendar date.
        if (value.Length == 10)
        {
            return DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }

        return true;
    }

    [GeneratedRegex(@"^\d{4}(-(0[1-9]|1[0-2])(-(0[1-9]|[12]\d|3[01]))?)?$")]
    private static partial Regex MyRegex();
}
