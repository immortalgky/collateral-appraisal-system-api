using Workflow.Sla.Models;

namespace Workflow.Sla.Services;

/// <summary>
/// An activity's computed SLA deadline together with its clock-start anchor (<see cref="StartAt"/>):
/// the point the budget runs from — AssignedAt for Assignment-anchored policies, the appointment for
/// AppointmentDate-anchored ones. Both null when no deadline applies. <see cref="StartAt"/> is persisted
/// so the at-risk monitor measures the 75% threshold from the real clock-start, not AssignedAt.
/// </summary>
public readonly record struct SlaDeadline(DateTime? DueAt, DateTime? StartAt);

/// <summary>
/// Result of window-governance resolution. A non-null instance means a Stage window governs the
/// activity's task: <see cref="DueAt"/> is the window deadline (null = appointment-anchored window
/// awaiting an appointment), <see cref="AnchorType"/> lets callers recompute only the
/// appointment-anchored windows on a reschedule, and <see cref="StartAt"/> is the window's clock-start
/// (start-activity entry, or the appointment) for the at-risk threshold.
/// </summary>
public record GoverningStageResult(DateTime? DueAt, SlaAnchorType AnchorType, DateTime? StartAt);

public interface ISlaCalculator
{
    /// <param name="correlationId">
    /// Workflow correlation ID (= Request.Id). When provided, the calculator looks up prior
    /// CompletedTask legs for (CorrelationId, ActivityId) to subtract already-consumed business
    /// time from the budget (rework does not grant a fresh full window).
    /// </param>
    /// <param name="appointmentDate">
    /// On-site visit date. Required when the resolved policy has AnchorType = AppointmentDate;
    /// null returns null DueAt ("awaiting appointment").
    /// </param>
    Task<SlaDeadline> CalculateActivityDueAtAsync(
        string activityId,
        Guid workflowDefinitionId,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        DateTime assignedAt,
        TimeSpan? defaultTimeout,
        DateTime? workflowDueAt,
        Guid? correlationId = null,
        DateTime? appointmentDate = null,
        CancellationToken ct = default);

    Task<DateTime?> CalculateWorkflowDueAtAsync(
        Guid workflowDefinitionId,
        string? loanType,
        string? appraisalType,
        DateTime startedOn,
        CancellationToken ct = default);

    Task<WorkflowSlaSnapshot?> GetWorkflowSlaSnapshotAsync(
        Guid workflowDefinitionId,
        string? loanType,
        string? appraisalType,
        DateTime startedOn,
        CancellationToken ct = default);

    /// <param name="correlationId">
    /// Workflow correlation ID. Reserved — a stage/window deadline is a fixed close (anchor + full
    /// budget) that never subtracts consumed time, so cumulative deduction does not apply at stage
    /// scope (it lives in <see cref="CalculateActivityDueAtAsync"/>).
    /// </param>
    /// <param name="appointmentDate">
    /// On-site visit date. Required when the resolved policy has AnchorType = AppointmentDate;
    /// null returns null DueAt ("awaiting appointment").
    /// </param>
    Task<DateTime?> CalculateStageDueAtAsync(
        Guid? workflowDefinitionId,
        string startActivityKey,
        DateTime startedAt,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        Guid? correlationId = null,
        DateTime? appointmentDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves whether <paramref name="activityId"/> is a member of a Stage-scope window and, if so,
    /// returns that window's governing deadline (anchored on the window's start-activity entry time).
    /// Returns null when the activity belongs to no window — the caller keeps its per-activity DueAt.
    /// </summary>
    Task<GoverningStageResult?> ResolveGoverningStageDueAtAsync(
        string activityId,
        Guid workflowDefinitionId,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        DateTime assignedAt,
        Guid? correlationId = null,
        DateTime? appointmentDate = null,
        CancellationToken ct = default);
}
