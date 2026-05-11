namespace Appraisal.Application.Features.Invoices.GetInvoiceList;

public record GetInvoiceListQuery(
    int PageNumber,
    int PageSize,
    string? Status,
    string? CompanySearch,
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? CompanyId
) : IQuery<PaginatedResult<InvoiceListDto>>;
