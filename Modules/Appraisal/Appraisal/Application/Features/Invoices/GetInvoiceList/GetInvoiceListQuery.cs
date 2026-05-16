namespace Appraisal.Application.Features.Invoices.GetInvoiceList;

public record GetInvoiceListQuery(
    int PageNumber,
    int PageSize,
    /// <summary>Single status value (Pending | Sent | Paid). Null = no status filter.</summary>
    string? Status,
    string? CompanySearch,
    DateOnly? SentDateFrom,
    DateOnly? SentDateTo,
    DateOnly? PaidDateFrom,
    DateOnly? PaidDateTo,
    string? Search,
    Guid? CompanyId,
    /// <summary>
    /// Optional group-by criterion. Currently supported: "company".
    /// Null/empty falls back to CreatedAt DESC. Future values can extend the ORDER BY map.
    /// </summary>
    string? GroupBy = null
) : IQuery<InvoiceListResult>;

public record InvoiceListResult(
    IReadOnlyList<InvoiceListDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int GrandItemCount,
    decimal GrandTotalAmount
);
