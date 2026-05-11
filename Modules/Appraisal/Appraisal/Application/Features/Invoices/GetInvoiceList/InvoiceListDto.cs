namespace Appraisal.Application.Features.Invoices.GetInvoiceList;

public record InvoiceListDto(
    Guid Id,
    string? InvoiceNumber,
    string Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime? PeriodStartDate,
    DateTime? PeriodEndDate,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    string? PaymentReference,
    string? PaymentMethod,
    DateTime? PaymentDate,
    Guid CompanyId,
    string? CompanyName,
    DateTime CreatedAt
);
