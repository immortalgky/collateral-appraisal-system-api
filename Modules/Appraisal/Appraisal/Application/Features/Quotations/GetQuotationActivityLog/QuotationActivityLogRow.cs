namespace Appraisal.Application.Features.Quotations.GetQuotationActivityLog;

public record QuotationActivityLogRow(
    Guid Id,
    Guid QuotationRequestId,
    Guid? CompanyQuotationId,
    Guid? CompanyId,
    string ActivityName,
    DateTime ActionAt,
    string ActionBy,
    string? ActionByRole,
    string? Remark);
