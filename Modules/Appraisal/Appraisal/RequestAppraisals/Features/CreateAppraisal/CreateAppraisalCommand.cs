namespace Appraisal.RequestAppraisals.Features.CreateAppraisal;

public record CreateAppraisalCommand(AppraisalDto Appraisal) : ICommand<CreateAppraisalResult>;