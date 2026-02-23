using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.ReorderPropertiesInGroup;

public record ReorderPropertiesInGroupCommand(
    Guid AppraisalId,
    Guid GroupId,
    List<Guid> OrderedPropertyIds
) : ICommand<ReorderPropertiesInGroupResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
