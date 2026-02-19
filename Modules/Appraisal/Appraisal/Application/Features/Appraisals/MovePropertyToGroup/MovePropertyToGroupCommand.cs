using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.MovePropertyToGroup;

public record MovePropertyToGroupCommand(
    Guid AppraisalId,
    Guid PropertyId,
    Guid TargetGroupId,
    int? TargetPosition
) : ICommand<MovePropertyToGroupResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
