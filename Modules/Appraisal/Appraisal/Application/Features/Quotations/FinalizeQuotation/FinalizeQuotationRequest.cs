using Appraisal.Application.Features.Quotations.AutoAssign;

namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public record FinalizeQuotationRequest(
    Guid CompanyQuotationId,
    string? Reason = null,
    AutoAssignRequest? AutoAssign = null);
