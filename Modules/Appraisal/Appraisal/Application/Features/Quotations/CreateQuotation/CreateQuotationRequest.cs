namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public record CreateQuotationRequest(
    DateTime DueDate,
    Guid RequestedBy,
    string RequestedByName,
    string? Description = null,
    string? SpecialRequirements = null);