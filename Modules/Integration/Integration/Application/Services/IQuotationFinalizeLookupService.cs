namespace Integration.Application.Services;

/// <summary>
/// Snapshot of a finalized quotation used to build the QUOTATION_FINALIZED webhook payload.
/// Property names serialize (camelCase) to the agreed external contract embedded in data.reason.
/// </summary>
public record QuotationFinalizeSnapshot(
    string? QuotationNumber,
    Guid CompanyQuotationId,
    string? ValuerName,
    decimal TotalAppraisalFee,
    int EstimatedDays,
    IReadOnlyList<QuotationFinalizeItem> Items);

public record QuotationFinalizeItem(
    string AppraisalNumber,
    decimal QuotedPrice,
    string? PropertyType,
    int EstimatedDays);

public interface IQuotationFinalizeLookupService
{
    Task<QuotationFinalizeSnapshot?> GetSnapshotAsync(
        Guid quotationRequestId,
        Guid winningQuotationId,
        CancellationToken ct = default);
}
