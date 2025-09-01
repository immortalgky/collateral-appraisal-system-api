using Appraisal.Service;
using Mapster;

namespace Appraisal.RequestAppraisals.Features.CreateAppraisalDetail;

internal class CreateAppraisalDetailCommandHandler(IAppraisalService appraisalService)
    : ICommandHandler<CreateAppraisalDetailCommand, CreateAppraisalDetailResult>
{
    public async Task<CreateAppraisalDetailResult> Handle(CreateAppraisalDetailCommand command, CancellationToken cancellationToken)
    {
        var appraisal = appraisalService.CreateRequestAppraisalDetail(command.Appraisal, command.RequestId, command.CollateralId);

        await appraisalService.AddRequestAppraisalDetailAsync(appraisal, cancellationToken);

        var result = appraisal.Adapt<RequestAppraisalDto>() with {ApprId = appraisal.Id};

        return new CreateAppraisalDetailResult(result);
    }

}