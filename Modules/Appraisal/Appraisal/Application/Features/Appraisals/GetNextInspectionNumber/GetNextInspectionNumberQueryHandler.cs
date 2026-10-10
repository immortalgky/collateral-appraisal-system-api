using Appraisal.Application.Features.Shared;
using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using MediatR;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetNextInspectionNumber;

public class GetNextInspectionNumberQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    ISender sender
) : IQueryHandler<GetNextInspectionNumberQuery, GetNextInspectionNumberResult>
{
    public async Task<GetNextInspectionNumberResult> Handle(
        GetNextInspectionNumberQuery query,
        CancellationToken cancellationToken)
    {
        // An external (company) caller only reads appraisals assigned to its company.
        await AppraisalAccessScope.EnsureCallerMayReadAsync(
            connectionFactory.GetOpenConnection(), currentUser, query.AppraisalId, cancellationToken);

        // The SAME query AppraisalCreationService stamps from, over the SAME chain (up to the root, then down over
        // every descendant), so picking an older book still gives the right next round.
        var chain = await sender.Send(new ResolveLatestInAppraisalChainQuery(query.AppraisalId), cancellationToken)
                    ?? throw new AppraisalNotFoundException(query.AppraisalId);

        return new GetNextInspectionNumberResult(chain.ProgressiveCount + 1);
    }
}
