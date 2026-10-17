using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

namespace Appraisal.Application.Services;

public static class DefaultAssignmentValidator
{
    /// <summary>
    /// Cleans the raw file contents. Unknown appraisal types and invalid values are logged and skipped;
    /// inactive entries are dropped. Valid values are stored in their canonical spelling; unset ones stay null.
    /// </summary>
    public static AssignmentDefaultsSnapshot Sanitize(
        IReadOnlyDictionary<string, AssignmentDefaultsEntry?> raw,
        ILogger logger
    )
    {
        var entries = new Dictionary<string, AssignmentDefaultsEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entry) in raw)
        {
            var appraisalType = AppraisalTypes.ValidValues.FirstOrDefault(t => string.Equals(t, key, StringComparison.OrdinalIgnoreCase));
            if (appraisalType is null)
            {
                logger.LogWarning("Assignment defaults: unknown appraisal type '{Key}' skipped", key);
                continue;
            }

            if (entry is null || !entry.IsActive)
                continue;

            entries[appraisalType] = new AssignmentDefaultsEntry(
                true,
                Pick(entry.AssignmentType, AssignmentDefaultValues.ValidTypes, appraisalType, "assignmentType", logger),
                Pick(entry.AssignmentMethod, AssignmentDefaultValues.ValidMethods, appraisalType, "assignmentMethod", logger),
                Pick(entry.InternalFollowupAssignmentMethod, AssignmentDefaultValues.ValidFollowupMethod,
                    appraisalType, "internalFollowupAssignmentMethod", logger));
        }

        return new AssignmentDefaultsSnapshot(entries);
    }

    // Matches any casing and returns the canonical spelling; null when unset or invalid.
    private static string? Pick(
        string? value,
        IReadOnlyList<string> allowed,
        string appraisalType,
        string field,
        ILogger logger
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var canonical = allowed.FirstOrDefault(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
            logger.LogWarning(
                "Assignment defaults: '{Value}' is not valid for {Type}.{Field} and was skipped",
                value, appraisalType, field);

        return canonical;
    }
}
