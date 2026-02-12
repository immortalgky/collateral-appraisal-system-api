using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Fees.CreateAppraisalFee;

public record CreateAppraisalFeeCommand(
    Guid AppraisalId,
    Guid AssignmentId,
    decimal? BankAbsorbAmount = null
) : ICommand<CreateAppraisalFeeResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
