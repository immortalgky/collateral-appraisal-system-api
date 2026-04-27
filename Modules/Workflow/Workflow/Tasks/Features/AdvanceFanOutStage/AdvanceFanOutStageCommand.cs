using Shared.CQRS;

namespace Workflow.Tasks.Features.AdvanceFanOutStage;

/// <summary>
/// Takes a user action on a fan-out PendingTask.
///
/// When the matched action defines <c>to: &lt;stageName&gt;</c> the handler advances the fan-out
/// item's stage state and reassigns the PendingTask in-place — the workflow stays paused.
///
/// When the matched action defines <c>complete: &lt;outcome&gt;</c> the handler completes the
/// fan-out item via the standard ResumeWorkflow path.
///
/// Falls through to the legacy no-stages path when the activity carries no <c>stages[]</c>.
///
/// Provide <see cref="PendingTaskId"/> when the caller already has it (action endpoint URL),
/// or supply <see cref="WorkflowInstanceId"/> + <see cref="ActivityId"/> + <see cref="CompanyId"/>
/// to let the handler resolve the task itself (used by feature handlers in other modules that
/// only know the workflow context, e.g. the quotation submit-to-checker flow).
/// </summary>
public record AdvanceFanOutStageCommand(
    Guid? PendingTaskId,
    string ActionValue,
    string CompletedBy,
    Dictionary<string, object>? AdditionalInput = null,
    Guid? WorkflowInstanceId = null,
    string? ActivityId = null,
    Guid? CompanyId = null
) : ICommand<AdvanceFanOutStageResult>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record AdvanceFanOutStageResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    /// <summary>true = stage transitioned but task still open; false = task completed/resumed</summary>
    bool StageAdvanced = false,
    string? NextStage = null
);
