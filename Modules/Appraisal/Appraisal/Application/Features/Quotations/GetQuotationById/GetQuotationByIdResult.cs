namespace Appraisal.Application.Features.Quotations.GetQuotationById;

public record GetQuotationByIdResult(
    Guid Id,
    string QuotationNumber,
    DateTime RequestDate,
    DateTime DueDate,
    string Status,
    Guid RequestedBy,
    string RequestedByName,
    string? Description,
    string? SpecialRequirements,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    int TotalQuotationsReceived,
    Guid? SelectedCompanyId,
    Guid? SelectedQuotationId,
    DateTime? SelectedAt,
    string? SelectionReason);