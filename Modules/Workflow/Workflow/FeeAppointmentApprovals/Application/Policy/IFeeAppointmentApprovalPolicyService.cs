namespace Workflow.FeeAppointmentApprovals.Application.Policy;

/// <summary>
/// Evaluates whether a fee/appointment change requires approval and which tier approver
/// should be assigned. Policy is driven by DB-backed configuration tables.
/// </summary>
public interface IFeeAppointmentApprovalPolicyService
{
    /// <summary>
    /// Returns true if the appointment change requires bank approval based on the
    /// AppointmentApprovalRule configuration (weekend/holiday, lead-time, reschedule count).
    /// </summary>
    Task<bool> RequiresAppointmentApprovalAsync(
        DateTime newDate,
        int rescheduleCount,
        string requestSource,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the matching FeeApprovalTier for the given total requested fee amount,
    /// or null if no approval is required (no matching active tier).
    /// </summary>
    Task<FeeApprovalTierMatch?> GetFeeTierAsync(
        decimal totalFeeAmount,
        string requestSource,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the lowest-priority (most permissive) active tier for the given requestSource.
    /// Used as the appointment-change approver when no fee lines are present.
    /// Returns a hardcoded fallback ("IntAdmin" group) if no tiers are seeded.
    /// </summary>
    Task<FeeApprovalTierMatch> GetLowestActiveTierAsync(
        string requestSource,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the strictest tier between appointment-tier and fee-tier (higher Priority = stricter).
    /// Returns null if neither requires approval.
    /// </summary>
    FeeApprovalTierMatch? PickStrictestTier(FeeApprovalTierMatch? appointmentTier, FeeApprovalTierMatch? feeTier);
}

/// <summary>
/// Projection of a resolved FeeApprovalTier row.
/// </summary>
public record FeeApprovalTierMatch(
    Guid TierId,
    string ApproverCode,
    string AssignedType,
    int Priority,
    string TierLabel);
