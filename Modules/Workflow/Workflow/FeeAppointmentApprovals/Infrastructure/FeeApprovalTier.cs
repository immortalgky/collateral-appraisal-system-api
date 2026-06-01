using Shared.DDD;

namespace Workflow.FeeAppointmentApprovals.Infrastructure;

/// <summary>
/// Fee approval tier configuration row.
/// Maps a total requested fee amount range to an approver (user or group).
/// Priority determines the strictest tier when multiple rules could apply.
/// </summary>
public class FeeApprovalTier : Entity<Guid>
{
    /// <summary>Minimum total requested fee amount (inclusive). Defaults to 0.</summary>
    public decimal MinAmount { get; private set; }

    /// <summary>Maximum total requested fee amount (inclusive). Null = no upper bound.</summary>
    public decimal? MaxAmount { get; private set; }

    /// <summary>User or group code of the approver resolved for this tier.</summary>
    public string ApproverCode { get; private set; } = default!;

    /// <summary>"1" = specific user, "2" = group/role</summary>
    public string AssignedType { get; private set; } = default!;

    /// <summary>Human-readable label for this tier (e.g. "IntAdmin", "Checker").</summary>
    public string TierLabel { get; private set; } = default!;

    /// <summary>Priority for strictest-tier selection. Higher = stricter.</summary>
    public int Priority { get; private set; }

    /// <summary>Whether this tier is active and matched during policy evaluation.</summary>
    public bool IsActive { get; private set; }

    /// <summary>"Ext", "Int", or "Both" — which RequestSource this tier applies to.</summary>
    public string AppliesTo { get; private set; } = "Ext";

    private FeeApprovalTier() { }

    public static FeeApprovalTier Create(
        decimal minAmount,
        decimal? maxAmount,
        string approverCode,
        string assignedType,
        string tierLabel,
        int priority,
        bool isActive = true,
        string appliesTo = "Ext")
    {
        return new FeeApprovalTier
        {
            Id = Guid.CreateVersion7(),
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            ApproverCode = approverCode,
            AssignedType = assignedType,
            TierLabel = tierLabel,
            Priority = priority,
            IsActive = isActive,
            AppliesTo = appliesTo
        };
    }

    public void Update(
        decimal minAmount,
        decimal? maxAmount,
        string approverCode,
        string assignedType,
        string tierLabel,
        int priority,
        bool isActive,
        string appliesTo)
    {
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        ApproverCode = approverCode;
        AssignedType = assignedType;
        TierLabel = tierLabel;
        Priority = priority;
        IsActive = isActive;
        AppliesTo = appliesTo;
    }
}
