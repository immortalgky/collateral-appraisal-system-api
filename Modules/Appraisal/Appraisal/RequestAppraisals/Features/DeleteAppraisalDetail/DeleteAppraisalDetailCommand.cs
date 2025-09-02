namespace Appraisal.RequestAppraisals.Features.DeleteAppraisalDetail;

public record DeleteAppraisalDetailCommand(long Id) : ICommand<DeleteAppraisalDetailResult>;