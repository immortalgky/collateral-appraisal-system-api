using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.LinkAppraisalComparable;

public record LinkAppraisalComparableCommand(
    Guid AppraisalId,
    Guid MarketComparableId,
    int SequenceNumber,
    decimal OriginalPricePerUnit,
    decimal? Weight,
    string? SelectionReason,
    string? Notes
) : ICommand<LinkAppraisalComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
