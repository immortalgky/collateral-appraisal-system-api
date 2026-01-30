using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateAppraisal;

/// <summary>
/// Command to create a new Appraisal
/// </summary>
public record CreateAppraisalCommand(
    Guid RequestId,
    string AppraisalType,
    string Priority,
    int? SLADays = null
) : ICommand<CreateAppraisalResult>, ITransactionalCommand<IAppraisalUnitOfWork>;