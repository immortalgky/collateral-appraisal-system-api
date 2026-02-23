using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdatePropertyGroup;

/// <summary>
/// Command to update a PropertyGroup
/// </summary>
public record UpdatePropertyGroupCommand(
    Guid AppraisalId,
    Guid GroupId,
    string GroupName,
    string? Description,
    bool UseSystemCalc
) : ICommand<UpdatePropertyGroupResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
