using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnlinkAppraisalComparable;

public class UnlinkAppraisalComparableCommandHandler(
    AppraisalDbContext dbContext
) : ICommandHandler<UnlinkAppraisalComparableCommand, UnlinkAppraisalComparableResult>
{
    public async Task<UnlinkAppraisalComparableResult> Handle(
        UnlinkAppraisalComparableCommand command,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.AppraisalComparables
            .FirstOrDefaultAsync(ac => ac.Id == command.AppraisalComparableId
                                    && ac.AppraisalId == command.AppraisalId, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException(
                $"Appraisal comparable with ID {command.AppraisalComparableId} not found for appraisal {command.AppraisalId}.");

        dbContext.AppraisalComparables.Remove(entity);

        return new UnlinkAppraisalComparableResult(true);
    }
}
