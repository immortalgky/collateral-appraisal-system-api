using Appraisal.Service;

namespace Appraisal.RequestAppraisals.Features.UpdateAppraisalDetail;

internal class UpdateAppraisalDetailCommandHandler(IAppraisalService appraisalService)
    : ICommandHandler<UpdateAppraisalDetailCommand, UpdateAppraisalDetailResult>
{
    public async Task<UpdateAppraisalDetailResult> Handle(UpdateAppraisalDetailCommand command, CancellationToken cancellationToken)
    {
        await appraisalService.GetRequestAppraisalDetailByIdAsync(command.Id, cancellationToken);

        var newAppraisalDetail = appraisalService.UpdateRequestAppraisalDetail(command.Appraisal);

        await appraisalService.UpdateRequestAppraisalDetailAsync(newAppraisalDetail, cancellationToken);

        return new UpdateAppraisalDetailResult(true);
    }
}
