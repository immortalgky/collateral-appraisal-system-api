using Shared.CQRS;

namespace Integration.Application.Features.Quotations.ApproveQuotation;

public record ApproveQuotationCommand(
    Guid QuotationId,
    string? ApprovalReason
) : ICommand<ApproveQuotationResult>;

public record ApproveQuotationResult(bool Success);
