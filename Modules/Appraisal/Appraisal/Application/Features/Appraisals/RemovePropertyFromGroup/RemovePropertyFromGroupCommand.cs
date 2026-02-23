using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.RemovePropertyFromGroup;

/// <summary>
/// Command to remove a property from a group
/// </summary>
public record RemovePropertyFromGroupCommand(
    Guid AppraisalId,
    Guid GroupId,
    Guid PropertyId
) : ICommand<RemovePropertyFromGroupResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
