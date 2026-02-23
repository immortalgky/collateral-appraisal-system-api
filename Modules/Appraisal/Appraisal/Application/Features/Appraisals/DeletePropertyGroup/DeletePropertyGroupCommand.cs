using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.DeletePropertyGroup;

/// <summary>
/// Command to delete a PropertyGroup
/// </summary>
public record DeletePropertyGroupCommand(
    Guid AppraisalId,
    Guid GroupId
) : ICommand<DeletePropertyGroupResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
