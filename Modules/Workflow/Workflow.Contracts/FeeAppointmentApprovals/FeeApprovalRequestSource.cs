namespace Workflow.Contracts.FeeAppointmentApprovals;

/// <summary>
/// Canonical values for fee/appointment approval routing. <see cref="External"/> and
/// <see cref="Internal"/> identify who raised a request; rule/tier rows additionally use
/// <see cref="Both"/> for their <c>AppliesTo</c> targeting.
/// </summary>
public static class FeeApprovalRequestSource
{
    /// <summary>External appraisal company (caller carries a company_id claim).</summary>
    public const string External = "Ext";

    /// <summary>Bank-internal user (no company_id claim).</summary>
    public const string Internal = "Int";

    /// <summary>Rule/tier AppliesTo value matching both external and internal requests.</summary>
    public const string Both = "Both";
}
