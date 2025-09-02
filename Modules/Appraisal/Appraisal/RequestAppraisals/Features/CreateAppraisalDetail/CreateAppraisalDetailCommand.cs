namespace Appraisal.RequestAppraisals.Features.CreateAppraisalDetail;

public record CreateAppraisalDetailCommand(RequestAppraisalDto Appraisal, long RequestId, long CollateralId) : ICommand<CreateAppraisalDetailResult>;