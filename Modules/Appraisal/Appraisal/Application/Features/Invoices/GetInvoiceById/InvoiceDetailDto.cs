namespace Appraisal.Application.Features.Invoices.GetInvoiceById;

public record InvoiceDetailDto(
    Guid Id,
    string? InvoiceNumber,
    string Status,
    decimal TotalAmount,
    string? CompanyName,
    string? Notes,
    string? PaymentReference,
    string? PaymentMethod,
    DateOnly? PaymentDate,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    DateTime? SubmittedAt,
    Guid CompanyId,
    List<InvoiceItemDto> Items
);

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
    DateTime? ReceivedDate
);
