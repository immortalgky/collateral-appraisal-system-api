namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetailById;

public class GetAppraisalDetailByIdQueryHandler(IAppraisalService appraisalService)
    : IQueryHandler<GetAppraisalDetailByIdQuery, GetAppraisalDetailByIdResult>
{
    public async Task<GetAppraisalDetailByIdResult> Handle(GetAppraisalDetailByIdQuery query, CancellationToken cancellationToken)
    {
        var appraisal = await appraisalService.GetRequestAppraisalDetailByIdAsync(query.Id, cancellationToken);

        var result = appraisal.Adapt<RequestAppraisalDto>() with {ApprId = appraisal.Id};

        return new GetAppraisalDetailByIdResult(result);
    }
}

