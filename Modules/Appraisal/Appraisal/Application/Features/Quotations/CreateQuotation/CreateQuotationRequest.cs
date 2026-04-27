namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public record CreateQuotationRequest(
    DateTime DueDate,
    string RequestedBy,
    string? Description = null,
    string? SpecialRequirements = null);