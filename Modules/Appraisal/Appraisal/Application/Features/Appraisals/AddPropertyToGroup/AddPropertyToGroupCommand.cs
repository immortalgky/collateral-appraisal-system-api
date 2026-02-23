using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.AddPropertyToGroup;

/// <summary>
/// Command to add a property to a group
/// </summary>
public record AddPropertyToGroupCommand(
    Guid AppraisalId,
    Guid GroupId,
    Guid PropertyId
) : ICommand<AddPropertyToGroupResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
