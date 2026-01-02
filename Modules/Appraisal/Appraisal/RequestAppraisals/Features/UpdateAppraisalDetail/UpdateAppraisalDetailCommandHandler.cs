namespace Appraisal.RequestAppraisals.Features.UpdateAppraisalDetail;

internal class UpdateAppraisalDetailCommandHandler(IAppraisalService appraisalService)
    : ICommandHandler<UpdateAppraisalDetailCommand, UpdateAppraisalDetailResult>
{
    public async Task<UpdateAppraisalDetailResult> Handle(UpdateAppraisalDetailCommand command, CancellationToken cancellationToken)
    {
        var newAppraisalDetail = await appraisalService.UpdateRequestAppraisalDetail(command.Appraisal, command.Id, cancellationToken);

        await appraisalService.UpdateRequestAppraisalDetailAsync(newAppraisalDetail, cancellationToken);

        return new UpdateAppraisalDetailResult(true);
    }
}
