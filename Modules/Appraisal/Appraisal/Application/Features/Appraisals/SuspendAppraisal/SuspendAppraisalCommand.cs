namespace Appraisal.Application.Features.Appraisals.SuspendAppraisal;

public record SuspendAppraisalCommand(Guid AppraisalId, bool IsSuspended, string Reason) : ICommand<SuspendAppraisalResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
