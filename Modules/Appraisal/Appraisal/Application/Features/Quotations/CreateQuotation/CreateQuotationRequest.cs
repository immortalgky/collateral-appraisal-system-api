namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public record CreateQuotationRequest(
    string QuotationNumber,
    DateTime DueDate,
    Guid RequestedBy,
    string RequestedByName,
    string? Description = null,
    string? SpecialRequirements = null);