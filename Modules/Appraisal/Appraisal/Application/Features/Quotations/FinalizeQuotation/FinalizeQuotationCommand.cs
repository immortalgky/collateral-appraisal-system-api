using Appraisal.Application.Configurations;
using Appraisal.Application.Features.Quotations.AutoAssign;

namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public record FinalizeQuotationCommand(
    Guid QuotationRequestId,
    Guid CompanyQuotationId,
    string? Reason = null,
    bool AutoAssignRequested = false,
    // Nullable elements only because the request body can contain nulls; the validator rejects them.
    IReadOnlyList<AutoAssignStaff?>? AutoAssignStaff = null)
    : ICommand<FinalizeQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
