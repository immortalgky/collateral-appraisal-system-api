using Appraisal.Service;
using Mapster;

namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetailByCollateralId;

public class GetAppraisalDetailByCollateralIdQueryHandler(IAppraisalService appraisalService)
    : IQueryHandler<GetAppraisalDetailByCollateralIdQuery, GetAppraisalDetailByCollateralIdResult>
{
    public async Task<GetAppraisalDetailByCollateralIdResult> Handle(GetAppraisalDetailByCollateralIdQuery query, CancellationToken cancellationToken)
    {
        var appraisals = await appraisalService.GetRequestAppraisalDetailByCollateralIdAsync(query.CollateralId, cancellationToken);

        var config = appraisalService.CreateRequestAppraisalDetailConfig();

        var result = appraisals.Adapt<List<RequestAppraisalDto>>(config);

        return new GetAppraisalDetailByCollateralIdResult(result);
    }
}