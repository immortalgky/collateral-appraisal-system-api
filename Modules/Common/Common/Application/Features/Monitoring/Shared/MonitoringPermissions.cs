namespace Common.Application.Features.Monitoring.Shared;

/// <summary>
/// Permission codes for the Monitoring feature (FSD §2.6.8).
/// Uppercase with colon-separated scope segments. Within-segment words use underscore.
/// Format: MODULE:SCREEN:LAYER[:TEAM]
/// Example: MONITORING:PENDING_INTERNAL:CHECKER
///
/// Layer-scoped screens (Pending Internal / External) accept an optional trailing :TEAM
/// modifier. Without it the monitor sees every staff's tasks in that stage; with it the
/// monitor sees only tasks assigned to members of their own auth.Teams team. The two can be
/// mixed across layers (e.g. CHECKER:TEAM + VERIFIER). See <see cref="MonitoringScopeService"/>.
/// </summary>
public static class MonitoringPermissions
{
    // ── Pending Quotation ─────────────────────────────────────────────────────
    public const string PendingQuotation = "MONITORING:PENDING_QUOTATION";

    // ── Pending Internal (layer-scoped) ───────────────────────────────────────
    public const string PendingInternalStaff = "MONITORING:PENDING_INTERNAL:STAFF";
    public const string PendingInternalChecker = "MONITORING:PENDING_INTERNAL:CHECKER";
    public const string PendingInternalVerifier = "MONITORING:PENDING_INTERNAL:VERIFIER";
    public const string PendingInternalApprover = "MONITORING:PENDING_INTERNAL:APPROVER";
    public const string PendingInternalAdmin = "MONITORING:PENDING_INTERNAL:ADMIN";

    // ── Pending External (layer-scoped) ───────────────────────────────────────
    public const string PendingExternalStaff = "MONITORING:PENDING_EXTERNAL:STAFF";
    public const string PendingExternalChecker = "MONITORING:PENDING_EXTERNAL:CHECKER";
    public const string PendingExternalVerifier = "MONITORING:PENDING_EXTERNAL:VERIFIER";
    public const string PendingExternalAdmin = "MONITORING:PENDING_EXTERNAL:ADMIN";

    // ── Pending Follow Up ─────────────────────────────────────────────────────
    public const string PendingFollowup = "MONITORING:PENDING_FOLLOWUP";

    // ── Pending Evaluation ────────────────────────────────────────────────────
    public const string PendingEvaluation = "MONITORING:PENDING_EVALUATION";

    // ── Meeting Follow Up ─────────────────────────────────────────────────────
    public const string MeetingFollowup = "MONITORING:MEETING_FOLLOWUP";

    // ── Policy prefixes (for prefix-match authorization + ViewPermissionPrefix on menu) ──
    public const string AllPrefix = "MONITORING:";
    public const string PendingInternalPrefix = "MONITORING:PENDING_INTERNAL:";
    public const string PendingExternalPrefix = "MONITORING:PENDING_EXTERNAL:";

    // ── Named policies ────────────────────────────────────────────────────────
    public const string PolicyPendingInternal = "monitoring.pending-internal";
    public const string PolicyPendingExternal = "monitoring.pending-external";
    public const string PolicyPendingQuotation = "monitoring.pending-quotation";
    public const string PolicyPendingFollowup = "monitoring.pending-followup";
    public const string PolicyPendingEvaluation = "monitoring.pending-evaluation";
    public const string PolicyMeetingFollowup = "monitoring.meeting-followup";
    public const string PolicyTopBreaches = "monitoring.top-breaches";
}
