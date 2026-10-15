namespace Reporting.Application.Models;

/// <summary>
/// View-model for the "Quotation Summary" report — a simple listing of the appraisals bundled
/// into a Request-for-Quotation, mirroring the table shown in the quotation email body.
/// Keyed by QuotationRequestId.
/// </summary>
public sealed class QuotationSummaryModel
{
    public IReadOnlyList<QuotationSummaryRowModel> Rows { get; init; } = Array.Empty<QuotationSummaryRowModel>();
}

/// <summary>One appraisal row in the quotation summary table.</summary>
public sealed class QuotationSummaryRowModel
{
    public int SeqNo { get; init; }
    public string AppraisalNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string PropertyType { get; init; } = string.Empty;
}
