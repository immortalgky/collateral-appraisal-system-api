namespace Appraisal.Application.Features.Invoices.GetInvoiceList;

public record GetInvoiceListQuery(
    int PageNumber,
    int PageSize,
    /// <summary>Single status value (Pending | Sent | Paid). Null = no status filter.</summary>
    string? Status,
    /// <summary>Status to exclude. Lets the Ext Unpaid tab show "everything but Paid" without
    /// requiring a CSV status filter on the include side.</summary>
    string? ExcludeStatus,
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
    string? GroupBy = null,
    /// <summary>
    /// Optional sort field. Whitelisted in the handler. Null = default ordering (CreatedAt DESC,
    /// or grouped by GroupBy when set). Group-by clustering is always preserved as the leading key.
    /// </summary>
    string? SortBy = null,
    /// <summary>"asc" | "desc". Defaults to "desc" so newest-first ordering is preserved.</summary>
    string? SortDir = null
) : IQuery<InvoiceListResult>;

public record InvoiceListResult(
    IReadOnlyList<InvoiceListDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int GrandItemCount,
    decimal GrandTotalAmount
);
