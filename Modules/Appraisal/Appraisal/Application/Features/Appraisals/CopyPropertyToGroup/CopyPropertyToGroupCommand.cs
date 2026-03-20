namespace Appraisal.Application.Features.Appraisals.CopyPropertyToGroup;

public record CopyPropertyToGroupCommand(
    Guid AppraisalId,
    Guid SourcePropertyId,
    Guid TargetGroupId
) : ICommand<CopyPropertyToGroupResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
