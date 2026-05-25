namespace Common.Application.Features.Monitoring.GetPendingQuotations;

public record PendingQuotationDto(
    Guid Id,
    string? QuotationNumber,
    string? Status,
    DateTime RequestDate,
    DateTime CutOffTime,
    string? RequestedBy,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    int TotalQuotationsReceived,
    string? RmUsername
);
