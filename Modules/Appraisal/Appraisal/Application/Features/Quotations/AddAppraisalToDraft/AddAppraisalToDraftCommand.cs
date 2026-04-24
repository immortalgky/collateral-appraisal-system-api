using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.AddAppraisalToDraft;

/// <summary>
/// Adds an appraisal to an existing Draft QuotationRequest.
/// Guards: status=Draft, caller owns the draft, appraisal not in another non-terminal quotation.
/// </summary>
public record AddAppraisalToDraftCommand(
    Guid QuotationRequestId,
    Guid AppraisalId,
    string AppraisalNumber,
    string PropertyType,
    string? PropertyLocation = null,
    decimal? EstimatedValue = null
) : ICommand<AddAppraisalToDraftResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
