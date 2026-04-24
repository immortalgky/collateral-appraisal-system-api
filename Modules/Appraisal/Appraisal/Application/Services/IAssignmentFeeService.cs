namespace Appraisal.Application.Services;

/// <summary>
/// Discriminated record that tells the fee service how to compute the fee amount at assignment time.
/// </summary>
public abstract record AssignmentFeeSource
{
    /// <summary>Look up the applicable tier from FeeStructures (FeeCode="01") using TotalSellingPrice.</summary>
    public sealed record TierBased : AssignmentFeeSource;

    /// <summary>Use the price agreed through the quotation process.</summary>
    public sealed record Quotation(decimal Amount, Guid QuotationRequestId) : AssignmentFeeSource;
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
}
