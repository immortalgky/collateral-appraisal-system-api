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
    string? PaymentOrderNo,
    DateTime? PaidDate,
    DateTime? SentDate,
    Guid CompanyId,
    string? CompanyName,
    // Thai name (null when absent); the client picks by its own locale. Position is load-bearing —
    // it must stay directly after CompanyName to match listColumns' order.
    string? CompanyNameLocal,
    DateTime CreatedAt
);
