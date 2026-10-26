namespace Appraisal.Application.Features.Quotations.AutoAssign;

/// <summary>
/// Runs AFTER the finalize transaction has committed. Deliberately not an <c>ITransactionalCommand</c>:
/// every appraisal is assigned in its own DI scope and its own workflow transaction, so one failure
/// cannot roll back the finalize or the other appraisals.
/// </summary>
public record AutoAssignQuotationAppraisalsCommand(
    Guid QuotationRequestId,
    IReadOnlyList<AutoAssignStaff> Staff)
    : ICommand<IReadOnlyList<AutoAssignItemResult>>;
