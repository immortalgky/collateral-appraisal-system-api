namespace Appraisal.Application.Services;

/// <summary>
/// Discriminated record that tells the fee service how to compute the fee amount at assignment time.
/// </summary>
public abstract record AssignmentFeeSource
{
    /// <summary>
    /// Look up the applicable tier from FeeStructures (FeeCode="01") using TotalSellingPrice.
    /// <paramref name="AppraisalType"/> selects the ladder: when the table holds rows scoped to
    /// that type they are used exclusively, otherwise the generic (null-scoped) ladder applies.
    /// Null means "no type known" and always resolves to the generic ladder.
    /// </summary>
    public sealed record TierBased(string? AppraisalType = null) : AssignmentFeeSource;

    /// <summary>
    /// Use the price agreed through the quotation process.
    /// <paramref name="Amount"/> must be the *ex-VAT* fee — the AppraisalFee aggregate
    /// recalculates VAT on top of item amounts, so passing a VAT-inclusive figure here
    /// would double-count the tax.
    /// <paramref name="QuotationNumber"/> is optional and used in the human-readable
    /// fee description.
    /// <paramref name="Amount"/> is null when the winner has no usable price: an admin's own base (01) outranks the
    /// quotation and needs no price. AssignAppraisal refuses an unpriced winner up front; if one still reaches the
    /// fee service (the admin's 01 was removed after Assign) it creates no base — never a 01 at 0 — and logs
    /// <paramref name="PriceError"/>, rather than throwing after the workflow has moved on.
    /// </summary>
    public sealed record Quotation(
        decimal? Amount, Guid QuotationRequestId, string? QuotationNumber = null, string? PriceError = null)
        : AssignmentFeeSource;

    /// <summary>
    /// Construction Inspection appraisal — the base fee is the inspection fee the PICKED prior appraisal
    /// carries (AppraisalFee.ConstructionInspectionFeeAmount) — nothing else. It bypasses the tier ladder
    /// entirely; only a quotation price outranks it.
    /// <paramref name="Amount"/> is null when the prior keyed no inspection fee — the service then adds no
    /// base item (no fallback to tier, none to the prior's own appraisal fee).
    /// <paramref name="PriorAppraisalNumber"/> is display data for the fee preview.
    /// </summary>
    public sealed record ConstructionInspection(
        decimal? Amount,
        string? PriorAppraisalNumber = null) : AssignmentFeeSource;
}

/// <summary>Where a resolved base appraisal fee came from (the fee preview's baseSource.kind).</summary>
public static class FeeBaseKinds
{
    public const string ConstructionInspectionFee = "ConstructionInspectionFee";
    public const string Tier = "Tier";
    public const string Quotation = "Quotation";
    public const string QuotationPending = "QuotationPending";
    public const string None = "None";
}

/// <summary>
/// The base (FeeCode "01") appraisal fee an <see cref="AssignmentFeeSource"/> resolves to, with the facts the
/// fee preview shows. <paramref name="Amount"/> is null when no base item would be created.
/// Tier fields are set only for <see cref="FeeBaseKinds.Tier"/>; the rest only for their own kind.
/// </summary>
public sealed record BaseFeeResolution(
    decimal? Amount,
    string Description,
    string Kind,
    string? PriorAppraisalNumber = null,
    string? QuotationNumber = null,
    decimal? SellingPrice = null,
    decimal? TierMin = null,
    decimal? TierMax = null);

/// <summary>
/// Materialises the fee item(s) on an <see cref="Appraisal.Domain.Appraisals.AppraisalFee"/> shell
/// at the moment the real assignee becomes known. Idempotent: never adds a second base (FeeCode "01") item,
/// re-prices a system base to the current source only when told this is a genuine (re)assignment, and leaves an
/// admin-entered base item untouched.
/// </summary>
public interface IAssignmentFeeService
{
    /// <param name="repriceSystemBase">
    /// True when the call comes from the assignment transition of a row that was Pending (first assignment, or a
    /// route-back to the admin queue that may now resolve a different source): an existing system base is then
    /// replaced by the current source's, and a full-absorb payment type is absorbed over the whole bill. False for a
    /// replay or a later route-back re-fire on an already-assigned row, which must leave existing items alone (and
    /// must not re-absorb items added after assignment). Required: a caller must decide, never default.
    /// </param>
    Task EnsureAssignmentFeeItemsAsync(
        Guid appraisalId,
        Guid assignmentId,
        AssignmentFeeSource source,
        bool repriceSystemBase,
        CancellationToken ct);

    /// <summary>
    /// Returns a CI-aware fee source: when the appraisal is a Progressive (construction inspection) follow-up
    /// with a PrevAppraisalId, reads the picked prior appraisal's inspection fee and returns
    /// <see cref="AssignmentFeeSource.ConstructionInspection"/>. A quotation source is returned unchanged for
    /// every type — the quoted price outranks the chained fee. A <see cref="AssignmentFeeSource.TierBased"/>
    /// default is stamped with the appraisal's type so the tier lookup can pick the type-scoped ladder.
    /// </summary>
    Task<AssignmentFeeSource> ResolveSourceForAppraisalAsync(
        Domain.Appraisals.Appraisal appraisal,
        AssignmentFeeSource defaultSource,
        CancellationToken ct);

    /// <summary>Same as the aggregate overload, for read paths that only hold the appraisal's identity.</summary>
    Task<AssignmentFeeSource> ResolveSourceForAppraisalAsync(
        string appraisalType,
        Guid? prevAppraisalId,
        AssignmentFeeSource defaultSource,
        CancellationToken ct);

    /// <summary>
    /// Pure resolution of the base fee a source yields, with no writes. <see cref="EnsureAssignmentFeeItemsAsync"/>
    /// materialises exactly this, so the fee preview cannot drift from what assignment will create.
    /// </summary>
    Task<BaseFeeResolution> ResolveBaseFeeAsync(
        AssignmentFeeSource source,
        decimal totalSellingPrice,
        CancellationToken ct);
}
