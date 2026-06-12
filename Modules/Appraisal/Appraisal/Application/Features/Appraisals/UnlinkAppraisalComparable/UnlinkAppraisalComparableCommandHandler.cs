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
            throw new NotFoundException("AppraisalComparable", command.AppraisalComparableId);

        dbContext.AppraisalComparables.Remove(entity);

        return new UnlinkAppraisalComparableResult(true);
    }
}
