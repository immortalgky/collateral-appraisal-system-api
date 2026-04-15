namespace Workflow.Tasks.Features.OpenTask;

public record OpenTaskCommand(Guid TaskId) : ICommand<OpenTaskResult>;

public record OpenTaskResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    Guid? AppraisalId = null,
    Guid? WorkflowInstanceId = null,
    string? TaskName = null
);
