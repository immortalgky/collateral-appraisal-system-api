namespace Appraisal.Application.Features.Quotations.AutoAssign;

/// <summary>
/// Live workflow + assignment state of appraisals, read with Dapper. Shared by the preview, the
/// auto-assign loop and the quotation detail so all three agree on what "ready" means.
/// </summary>
public interface IQuotationAutoAssignStateReader
{
    Task<IReadOnlyList<AppraisalAssignState>> GetAsync(
        IReadOnlyCollection<Guid> appraisalIds, CancellationToken cancellationToken);

    /// <summary>Appraisal ids linked to the quotation, or null when the quotation does not exist.</summary>
    Task<IReadOnlyList<Guid>?> GetQuotationAppraisalIdsAsync(Guid quotationId, CancellationToken cancellationToken);

    /// <summary>True when the workflow instance is the main appraisal workflow of this appraisal's request.</summary>
    Task<bool> WorkflowInstanceBelongsToAppraisalAsync(
        Guid appraisalId, Guid workflowInstanceId, CancellationToken cancellationToken);

    /// <summary>True when the quotation was created on its own, not started from an appraisal task.</summary>
    Task<bool> IsStandaloneAsync(Guid quotationId, CancellationToken cancellationToken);

    /// <summary>
    /// The quotation's winner: the selected one once finalized, else the tentative one that finalize will use.
    /// Null fields when there is none.
    /// </summary>
    Task<QuotationWinner> GetWinnerAsync(Guid quotationId, CancellationToken cancellationToken);

    /// <summary>
    /// Appraisal ids the given company quotation has a USABLE price for, by the same rule as
    /// CompanyAssignedIntegrationEventHandler.ResolveFeeSourceAsync.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetPricedAppraisalIdsAsync(
        Guid companyQuotationId, CancellationToken cancellationToken);
}

public sealed record QuotationWinner(Guid? CompanyQuotationId, Guid? CompanyId);

public sealed record AppraisalAssignState
{
    public const string AssignmentActivityId = "appraisal-assignment";
    public const string ExtAssignmentActivityId = "ext-appraisal-assignment";

    public Guid AppraisalId { get; init; }
    public string? AppraisalNumber { get; init; }
    public bool IsPma { get; init; }
    public string? AppraisalType { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string? WorkflowStatus { get; init; }
    public Guid? PendingTaskId { get; init; }
    public string? PendingActivityId { get; init; }
    public string? WorkingBy { get; init; }

    /// <summary>Holder of the pending task; a person when <see cref="TaskAssignedType"/> is not "2" (pool).</summary>
    public string? TaskAssignedTo { get; init; }
    public string? TaskAssignedType { get; init; }

    /// <summary>The pending task has an Open document follow-up, which blocks a normal completion.</summary>
    public bool HasOpenFollowup { get; init; }
    public string? AssignmentStatus { get; init; }
    public string? FollowupStaffId { get; init; }
    public string? FollowupStaffName { get; init; }
    public string? FollowupMethod { get; init; }

    /// <summary>The current assignment's fee already holds the admin's own base (01) above 0: it outranks the quotation price.</summary>
    public bool HasAdminBaseFee { get; init; }

    /// <summary>User who has the pending appraisal-assignment task open (lock), if not <paramref name="user"/>.</summary>
    public string? LockedByOther(string? user) =>
        PendingActivityId == AssignmentActivityId && !string.IsNullOrEmpty(WorkingBy)
                                                  && !string.Equals(WorkingBy, user, StringComparison.OrdinalIgnoreCase)
            ? WorkingBy
            : null;

    /// <summary>Person the pending appraisal-assignment task is personally assigned to, if not <paramref name="user"/>.</summary>
    public string? AssignedToOther(string? user) =>
        PendingActivityId == AssignmentActivityId && TaskAssignedType != "2" && !string.IsNullOrEmpty(TaskAssignedTo)
                                                  && !string.Equals(TaskAssignedTo, user, StringComparison.OrdinalIgnoreCase)
            ? TaskAssignedTo
            : null;

    public bool WorkflowActive => WorkflowInstanceId is not null && WorkflowStatus is not ("Cancelled" or "Completed" or "Failed");

    public bool WorkflowEnded => !WorkflowActive;
}
