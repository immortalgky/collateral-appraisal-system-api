namespace Appraisal.Application.Features.Invoices.GetInvoiceById;

public record InvoiceDetailDto(
    Guid Id,
    string? InvoiceNumber,
    string Status,
    decimal TotalAmount,
    string? CompanyName,
    // Thai name (null when absent); the client picks by its own locale. Position is load-bearing —
    // it must stay directly after CompanyName to match headerSql's column order.
    string? CompanyNameLocal,
    string? BankAccountNo,
    string? BankAccountName,
    string? Notes,
    string? PaymentOrderNo,
    DateTime? PaidDate,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    DateTime? SubmittedAt,
    Guid CompanyId)
{
    // Populated separately after the header is materialized; not part of the
    // ctor so Dapper can match the view columns positionally.
    public List<InvoiceItemDto> Items { get; init; } = new();
}

public record InvoiceItemDto(
    Guid Id,
    Guid AssignmentId,
    string? AppraisalNumber,
    string? CustomerName,
    string? ProductType,
    decimal FeeBeforeVAT,
    decimal VATRate,
    decimal VATAmount,
    decimal TotalFeeAfterVAT,
    decimal BankAbsorbAmount,
    decimal PayPartialAmount,
    decimal RemainingFee,
    DateTime? LastPaymentDate,
    DateTime? SubmittedDate
);
