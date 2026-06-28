using Shared.DDD;

namespace Workflow.Sla.Models;

public enum SlaPolicyScope { Activity = 1, Stage = 2, Workflow = 3 }

/// <summary>
/// Determines which timestamp starts the SLA clock for a given policy.
/// Assignment = clock starts when the task is assigned (AssignedAt).
/// AppointmentDate = clock starts when the on-site visit occurs (appointment date).
/// Null on an existing row is treated as Assignment (backward compatible).
/// </summary>
public enum SlaAnchorType { Assignment = 0, AppointmentDate = 1 }

public class SlaPolicy : Entity<Guid>
{
    // Existing fields — unchanged semantics for Activity scope.
    public string ActivityId { get; private set; } = default!;
    public Guid? WorkflowDefinitionId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string? LoanType { get; private set; }

    // New target dimension: null = any (wildcard). Values from AppraisalTypes
    // (New/ReAppraisal/Progressive/PreAppraisal). AssignmentType is a deferred future axis.
    public string? AppraisalType { get; private set; }

    public int DurationHours { get; private set; }
    public bool UseBusinessDays { get; private set; }
    public int Priority { get; private set; }

    // New: defaults to Activity so all existing rows continue to work.
    public SlaPolicyScope Scope { get; private set; } = SlaPolicyScope.Activity;

    // Stage scope only — clock starts when the workflow enters StartActivityKey.
    public string? StartActivityKey { get; private set; }
    public string? EndActivityKey { get; private set; }

    // Documentation only: middle activity keys in the stage span (stored as JSON).
    public string? MiddleActivityKeys { get; private set; }

    /// <summary>
    /// Configurable anchor for the SLA clock. Null = Assignment (preserves existing behaviour).
    /// AppointmentDate means the deadline is computed from the on-site visit date, not AssignedAt.
    /// </summary>
    public SlaAnchorType? AnchorType { get; private set; }

    private SlaPolicy() { }

    public static SlaPolicy Create(
        string activityId,
        int durationHours,
        bool useBusinessDays,
        int priority,
        Guid? workflowDefinitionId = null,
        Guid? companyId = null,
        string? loanType = null,
        SlaPolicyScope scope = SlaPolicyScope.Activity,
        string? startActivityKey = null,
        string? endActivityKey = null,
        string? middleActivityKeys = null,
        string? appraisalType = null,
        SlaAnchorType? anchorType = null)
    {
        ValidateScopeFields(scope, activityId, startActivityKey, endActivityKey, workflowDefinitionId);

        return new SlaPolicy
        {
            Id = Guid.CreateVersion7(),
            ActivityId = activityId,
            WorkflowDefinitionId = workflowDefinitionId,
            CompanyId = companyId,
            LoanType = loanType,
            AppraisalType = appraisalType,
            DurationHours = durationHours,
            UseBusinessDays = useBusinessDays,
            Priority = priority,
            Scope = scope,
            StartActivityKey = startActivityKey,
            EndActivityKey = endActivityKey,
            MiddleActivityKeys = middleActivityKeys,
            AnchorType = anchorType
        };
    }

    public void Update(
        int durationHours,
        bool useBusinessDays,
        int priority,
        string? loanType = null,
        Guid? companyId = null,
        SlaPolicyScope? scope = null,
        string? startActivityKey = null,
        string? endActivityKey = null,
        string? middleActivityKeys = null,
        Guid? workflowDefinitionId = null,
        string? appraisalType = null,
        SlaAnchorType? anchorType = null)
    {
        var effectiveScope = scope ?? Scope;
        var effectiveActivityId = ActivityId;
        var effectiveStartKey = startActivityKey ?? StartActivityKey;
        var effectiveEndKey = endActivityKey ?? EndActivityKey;
        var effectiveWorkflowId = workflowDefinitionId ?? WorkflowDefinitionId;

        ValidateScopeFields(effectiveScope, effectiveActivityId, effectiveStartKey, effectiveEndKey, effectiveWorkflowId);

        DurationHours = durationHours;
        UseBusinessDays = useBusinessDays;
        Priority = priority;
        LoanType = loanType;
        AppraisalType = appraisalType;
        CompanyId = companyId;
        AnchorType = anchorType;

        if (scope.HasValue) Scope = scope.Value;
        if (startActivityKey is not null) StartActivityKey = startActivityKey;
        if (endActivityKey is not null) EndActivityKey = endActivityKey;
        if (middleActivityKeys is not null) MiddleActivityKeys = middleActivityKeys;
        if (workflowDefinitionId.HasValue) WorkflowDefinitionId = workflowDefinitionId;
    }

    /// <summary>
    /// Stamps the anchor type on an existing row — used by the seeder to upgrade existing
    /// default policies without re-running the full Update validation path.
    /// </summary>
    public void SetAnchorType(SlaAnchorType? anchorType) => AnchorType = anchorType;

    /// <summary>
    /// Validates that the supplied scope-specific fields are consistent with the given scope.
    /// Throws <see cref="ArgumentException"/> when a required field is missing or an invalid
    /// combination is detected, preventing resolver-invisible rows from being persisted.
    /// </summary>
    private static void ValidateScopeFields(
        SlaPolicyScope scope,
        string? activityId,
        string? startActivityKey,
        string? endActivityKey,
        Guid? workflowDefinitionId)
    {
        switch (scope)
        {
            case SlaPolicyScope.Activity:
                if (string.IsNullOrWhiteSpace(activityId))
                    throw new ArgumentException(
                        "ActivityId is required for Activity-scope SlaPolicy.", nameof(activityId));
                // Activity scope must not carry Stage-specific keys.
                if (!string.IsNullOrWhiteSpace(startActivityKey))
                    throw new ArgumentException(
                        "StartActivityKey must be null for Activity-scope SlaPolicy.", nameof(startActivityKey));
                if (!string.IsNullOrWhiteSpace(endActivityKey))
                    throw new ArgumentException(
                        "EndActivityKey must be null for Activity-scope SlaPolicy.", nameof(endActivityKey));
                break;

            case SlaPolicyScope.Stage:
                if (string.IsNullOrWhiteSpace(startActivityKey))
                    throw new ArgumentException(
                        "StartActivityKey is required for Stage-scope SlaPolicy.", nameof(startActivityKey));
                if (string.IsNullOrWhiteSpace(endActivityKey))
                    throw new ArgumentException(
                        "EndActivityKey is required for Stage-scope SlaPolicy.", nameof(endActivityKey));
                // Stage scope's resolver looks up by StartActivityKey, not ActivityId. The DB column
                // is NOT NULL, so callers must use the "*" backfill sentinel (same pattern as Workflow scope).
                if (!string.IsNullOrWhiteSpace(activityId) && activityId != "*")
                    throw new ArgumentException(
                        "ActivityId must be null (or the \"*\" sentinel) for Stage-scope SlaPolicy.", nameof(activityId));
                break;

            case SlaPolicyScope.Workflow:
                if (!workflowDefinitionId.HasValue)
                    throw new ArgumentException(
                        "WorkflowDefinitionId is required for Workflow-scope SlaPolicy.", nameof(workflowDefinitionId));
                // Workflow scope must not carry Activity- or Stage-specific fields.
                // Exception: ActivityId == "*" is the backfill sentinel used by the migration
                // (AddSlaPolicyScopedUniqueIndexes); rows with that value are valid in storage.
                if (!string.IsNullOrWhiteSpace(activityId) && activityId != "*")
                    throw new ArgumentException(
                        "ActivityId must be null (or the \"*\" backfill sentinel) for Workflow-scope SlaPolicy.", nameof(activityId));
                if (!string.IsNullOrWhiteSpace(startActivityKey))
                    throw new ArgumentException(
                        "StartActivityKey must be null for Workflow-scope SlaPolicy.", nameof(startActivityKey));
                if (!string.IsNullOrWhiteSpace(endActivityKey))
                    throw new ArgumentException(
                        "EndActivityKey must be null for Workflow-scope SlaPolicy.", nameof(endActivityKey));
                break;

            default:
                throw new ArgumentException($"Unknown SlaPolicyScope value '{scope}'.", nameof(scope));
        }
    }
}
