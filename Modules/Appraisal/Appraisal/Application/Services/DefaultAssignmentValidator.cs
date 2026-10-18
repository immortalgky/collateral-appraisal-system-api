using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

namespace Appraisal.Application.Services;

public static class DefaultAssignmentValidator
{
    /// <summary>
    /// Cleans the raw file contents. Unknown appraisal types and invalid values are logged and skipped;
    /// inactive entries are dropped. Valid values are stored in their canonical spelling; unset ones stay null.
    /// Keys that name the same type (same key, or differing only by casing or surrounding whitespace): the
    /// first in file order wins and the rest are logged and skipped.
    /// </summary>
    public static AssignmentDefaultsSnapshot Sanitize(
        IEnumerable<KeyValuePair<string, AssignmentDefaultsEntry?>> raw,
        ILogger logger
    )
    {
        var entries = new Dictionary<string, AssignmentDefaultsEntry>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entry) in raw)
        {
            var appraisalType = Canonical(key, AppraisalTypes.ValidValues);
            if (appraisalType is null)
            {
                logger.LogWarning("Assignment defaults: unknown appraisal type '{Key}' skipped", key);
                continue;
            }

            // Checked before IsActive: an inactive duplicate is still a mistake in the file.
            if (!seen.Add(appraisalType))
            {
                logger.LogWarning(
                    "Assignment defaults: '{Key}' duplicates an earlier {Type} entry and was skipped", key, appraisalType);
                continue;
            }

            if (entry is null || !entry.IsActive)
                continue;

            var type = Pick(entry.AssignmentType, AssignmentDefaultValues.ValidTypes, appraisalType, "assignmentType", logger);
            var method = Pick(entry.AssignmentMethod, AssignmentDefaultValues.ValidMethods, appraisalType, "assignmentMethod", logger);
            if (type == AssignmentDefaultValues.TypeInternal && method == AssignmentDefaultValues.MethodQuotation)
                logger.LogWarning(
                    "Assignment defaults: {Type} is Internal but Quotation is External-only; Manual will be used",
                    appraisalType);

            entries[appraisalType] = new AssignmentDefaultsEntry(
                true,
                type,
                method,
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

        var canonical = Canonical(value, allowed);
        if (canonical is null)
            logger.LogWarning(
                "Assignment defaults: '{Value}' is not valid for {Type}.{Field} and was skipped",
                value, appraisalType, field);

        return canonical;
    }

    // Trim + case-insensitive match; returns the canonical spelling or null.
    private static string? Canonical(string? value, IReadOnlyList<string> allowed) =>
        allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
