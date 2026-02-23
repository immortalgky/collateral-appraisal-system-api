using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreatePropertyGroup;

/// <summary>
/// Command to create a new PropertyGroup
/// </summary>
public record CreatePropertyGroupCommand(
    Guid AppraisalId,
    string GroupName,
    string? Description = null
) : ICommand<CreatePropertyGroupResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
