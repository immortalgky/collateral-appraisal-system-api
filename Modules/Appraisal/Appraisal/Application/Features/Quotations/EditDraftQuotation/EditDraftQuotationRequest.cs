namespace Appraisal.Application.Features.Quotations.EditDraftQuotation;

public record EditDraftQuotationRequest(DateTime DueDate, IReadOnlyList<Guid> CompanyIds);
