namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetail;

public class GetAppraisalDetailQueryHandler(
    IAppraisalService appraisalService
) : IQueryHandler<GetAppraisalDetailQuery, GetAppraisalDetailResult>
{
    public async Task<GetAppraisalDetailResult> Handle(GetAppraisalDetailQuery query, CancellationToken cancellationToken)
    {
        var appraisal = await appraisalService.GetRequestAppraisalDetailAsync(query.PaginationRequest, cancellationToken);

        var config = appraisalService.CreateRequestAppraisalDetailConfig();

        var result = appraisal.Adapt<PaginatedResult<RequestAppraisalDto>>(config);

        return new GetAppraisalDetailResult(result);
    }
}