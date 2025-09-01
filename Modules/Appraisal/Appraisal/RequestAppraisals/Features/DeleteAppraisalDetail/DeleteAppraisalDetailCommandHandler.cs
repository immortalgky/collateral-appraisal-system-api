using Appraisal.Data.Repository;
using Appraisal.Service;

namespace Appraisal.RequestAppraisals.Features.DeleteAppraisalDetail;

public class DeleteAppraisalDetailCommandHandler(IAppraisalService appraisalService)
    : ICommandHandler<DeleteAppraisalDetailCommand, DeleteAppraisalDetailResult>
{
    public async Task<DeleteAppraisalDetailResult> Handle(DeleteAppraisalDetailCommand command, CancellationToken cancellationToken)
    {
        await appraisalService.DeleteRequestAppraisalDetailAsync(command.Id, cancellationToken);

        return new DeleteAppraisalDetailResult(true);
    }
}