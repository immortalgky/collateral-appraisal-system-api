namespace Appraisal.Domain.Appraisals;

/// <summary>
/// String constants for appraisal types used across the system.
/// Use these constants instead of magic strings; the matching DB migration
/// will rename legacy values (e.g. "Initial" → "New") in Phase 3.
/// </summary>
public static class AppraisalTypes
{
    public const string New = "New";
    public const string ReAppraisal = "ReAppraisal";
    public const string Progressive = "Progressive";
    public const string PreAppraisal = "PreAppraisal";

    public static readonly IReadOnlyList<string> ValidValues =
        [New, ReAppraisal, Progressive, PreAppraisal];

    /// <summary>Returns true if <paramref name="value"/> is one of the four recognised appraisal types.</summary>
    public static bool IsValid(string? value) =>
        value is not null && ValidValues.Contains(value);
}
