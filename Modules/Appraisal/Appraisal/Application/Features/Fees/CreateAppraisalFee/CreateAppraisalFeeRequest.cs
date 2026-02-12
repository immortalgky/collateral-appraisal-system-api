namespace Appraisal.Application.Features.Fees.CreateAppraisalFee;

public record CreateAppraisalFeeRequest(
    Guid AssignmentId,
    decimal? BankAbsorbAmount = null);
