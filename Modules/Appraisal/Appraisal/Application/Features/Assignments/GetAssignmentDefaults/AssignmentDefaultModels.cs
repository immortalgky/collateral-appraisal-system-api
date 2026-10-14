namespace Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

public static class AssignmentDefaultValues
{
    // Same spellings as the AppraisalAssignment columns. External-Offline is stored as External + method
    // "Offline" and chosen per case, so it has no default.

    // assignment type
    public const string TypeInternal = "Internal";
    public const string TypeExternal = "External";

    // assignment method
    public const string MethodManual = "Manual";
    public const string MethodRoundRobin = "RoundRobin";
    public const string MethodQuotation = "Quotation";

    // follow-up staff method - applicable when the type is "External"
    public const string FollowupManualMethod = "Manual";
    public const string FollowupRoundRobinMethod = "RoundRobin";

    public static readonly IReadOnlyList<string> ValidTypes = [TypeInternal, TypeExternal];
    public static readonly IReadOnlyList<string> ValidMethods = [MethodManual, MethodRoundRobin, MethodQuotation];
    public static readonly IReadOnlyList<string> ValidFollowupMethod = [FollowupManualMethod, FollowupRoundRobinMethod];

    public static readonly ResolvedAssignmentDefaults Baseline = new(TypeExternal, MethodQuotation, FollowupRoundRobinMethod);
}

// One JSON entry. A field left out of the file is unset (IsActive defaults to true).
public record AssignmentDefaultsEntry(
    bool IsActive = true,
    string? AssignmentType = null,
    string? AssignmentMethod = null,
    string? InternalFollowupAssignmentMethod = null
);

public sealed class AssignmentDefaultsSnapshot(IReadOnlyDictionary<string, AssignmentDefaultsEntry> entries)
{
    public static readonly AssignmentDefaultsSnapshot Empty = new(new Dictionary<string, AssignmentDefaultsEntry>());

    public IReadOnlyDictionary<string, AssignmentDefaultsEntry> Entries { get; } =
        new Dictionary<string, AssignmentDefaultsEntry>(entries, StringComparer.OrdinalIgnoreCase);
}

public record ResolvedAssignmentDefaults(
    string AssignmentType,
    string AssignmentMethod,
    string InternalFollowupAssignmentMethod
);
