using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.LinkAppraisalComparable;

public class LinkAppraisalComparableCommandHandler(
    AppraisalDbContext dbContext
) : ICommandHandler<LinkAppraisalComparableCommand, LinkAppraisalComparableResult>
{
    public async Task<LinkAppraisalComparableResult> Handle(
        LinkAppraisalComparableCommand command,
        CancellationToken cancellationToken)
    {
        var appraisalExists = await dbContext.Appraisals
            .AnyAsync(a => a.Id == command.AppraisalId, cancellationToken);

        if (!appraisalExists)
            throw new InvalidOperationException($"Appraisal with ID {command.AppraisalId} not found.");

        var comparableExists = await dbContext.MarketComparables
            .AnyAsync(mc => mc.Id == command.MarketComparableId, cancellationToken);

        if (!comparableExists)
            throw new InvalidOperationException($"Market comparable with ID {command.MarketComparableId} not found.");

        var isDuplicate = await dbContext.AppraisalComparables
            .AnyAsync(ac => ac.AppraisalId == command.AppraisalId
                         && ac.MarketComparableId == command.MarketComparableId, cancellationToken);

        if (isDuplicate)
            throw new InvalidOperationException("This market comparable is already linked to the appraisal.");

        var entity = AppraisalComparable.Create(
            command.AppraisalId,
            command.MarketComparableId,
            command.SequenceNumber,
            command.OriginalPricePerUnit,
            command.Weight ?? 0);

        if (command.SelectionReason is not null)
            entity.SetSelectionReason(command.SelectionReason);

        dbContext.AppraisalComparables.Add(entity);

        return new LinkAppraisalComparableResult(
            entity.Id,
            entity.SequenceNumber,
            entity.OriginalPricePerUnit,
            entity.Weight);
    }
}
