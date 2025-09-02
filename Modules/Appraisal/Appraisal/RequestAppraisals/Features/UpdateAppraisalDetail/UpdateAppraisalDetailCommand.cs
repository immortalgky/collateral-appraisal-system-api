namespace Appraisal.RequestAppraisals.Features.UpdateAppraisalDetail;

public record UpdateAppraisalDetailCommand(long Id, RequestAppraisalDto Appraisal) : ICommand<UpdateAppraisalDetailResult>;
