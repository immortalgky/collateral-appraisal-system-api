using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

namespace Appraisal.Application.Services;

public static class DefaultAssignmentResolver
{
    public static GetAssignmentDefaultsResult Resolve(AssignmentDefaultsSnapshot snapshot, string? appraisalType)
    {
        var baseline = AssignmentDefaultValues.Baseline;

        if (string.IsNullOrEmpty(appraisalType) || !snapshot.Entries.TryGetValue(appraisalType, out var entry))
            return baseline;

        // Per field: whatever the entry leaves out comes from the built-in defaults.
        var type = string.IsNullOrEmpty(entry.AssignmentType) ? baseline.AssignmentType : entry.AssignmentType;
        var method = string.IsNullOrEmpty(entry.AssignmentMethod) ? baseline.AssignmentMethod : entry.AssignmentMethod;
        var followup = string.IsNullOrEmpty(entry.InternalFollowupAssignmentMethod)
            ? baseline.InternalFollowupAssignmentMethod
            : entry.InternalFollowupAssignmentMethod;

        // Quotation is External-only (the baseline method is Quotation, so a type set alone can hit this).
        if (method == AssignmentDefaultValues.MethodQuotation && type != AssignmentDefaultValues.TypeExternal)
            method = AssignmentDefaultValues.MethodManual;

        return new GetAssignmentDefaultsResult(type, method, followup);
    }
}
