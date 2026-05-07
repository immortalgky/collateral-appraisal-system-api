namespace Appraisal.Application.Services;

/// <summary>
/// Discriminated record that tells the fee service how to compute the fee amount at assignment time.
/// </summary>
public abstract record AssignmentFeeSource
{
    /// <summary>Look up the applicable tier from FeeStructures (FeeCode="01") using TotalSellingPrice.</summary>
    public sealed record TierBased : AssignmentFeeSource;

    /// <summary>
    /// Use the price agreed through the quotation process.
    /// <paramref name="Amount"/> must be the *ex-VAT* fee — the AppraisalFee aggregate
    /// recalculates VAT on top of item amounts, so passing a VAT-inclusive figure here
    /// would double-count the tax.
    /// <paramref name="QuotationNumber"/> is optional and used in the human-readable
    /// fee description.
    /// </summary>
    public sealed record Quotation(decimal Amount, Guid QuotationRequestId, string? QuotationNumber = null) : AssignmentFeeSource;

    /// <summary>
    /// Construction Inspection appraisal — the fee is seeded from the prior engagement's
    /// CI fee captured on CollateralEngagement.ConstructionInspectionFeeAmount. CI bypasses
    /// the normal tier/quotation pipeline entirely.
    /// <paramref name="Amount"/> may be null when no prior engagement carries a CI fee — the
    /// service then leaves the fee items empty (no fallback to tier).
    /// </summary>
    public sealed record ConstructionInspection(decimal? Amount) : AssignmentFeeSource;
}

/// <summary>
/// Materialises the fee item(s) on an <see cref="Appraisal.Domain.Appraisals.AppraisalFee"/> shell
/// at the moment the real assignee becomes known. Idempotent: exits early when items already exist.
/// </summary>
public interface IAssignmentFeeService
{
    Task EnsureAssignmentFeeItemsAsync(
        Guid appraisalId,
        Guid assignmentId,
        AssignmentFeeSource source,
        CancellationToken ct);

    /// <summary>
    /// Returns a CI-aware fee source: when the appraisal is a Construction Inspection follow-up
    /// (AppraisalType=ConstructionInspection AND PrevAppraisalId is set), looks up the prior
    /// engagement's CI fee via Collateral and returns <see cref="AssignmentFeeSource.ConstructionInspection"/>.
    /// Otherwise returns <paramref name="defaultSource"/> unchanged.
    /// </summary>
    Task<AssignmentFeeSource> ResolveSourceForAppraisalAsync(
        Domain.Appraisals.Appraisal appraisal,
        AssignmentFeeSource defaultSource,
        CancellationToken ct);
}
