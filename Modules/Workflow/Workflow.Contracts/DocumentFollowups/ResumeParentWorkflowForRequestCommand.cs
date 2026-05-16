namespace Workflow.Contracts.DocumentFollowups;

/// <summary>
/// Resumes the parent appraisal workflow for the given request (data-fix route-back branch).
/// Looks up the workflow instance via CorrelationId = requestId.ToString().
/// If the instance is not at the expected activity, logs a warning and skips resume
/// (the data sync has already committed). This is a fire-and-tolerate-skip operation.
/// </summary>
public record ResumeParentWorkflowForRequestCommand(
    Guid RequestId,
    string Actor)
    : ICommand<Unit>, ITransactionalCommand<IWorkflowUnitOfWork>;
