namespace Workflow.Tasks.Features.ReassignTask;

public record ReassignTaskCommand(Guid TaskId, string NewAssignedTo) : ICommand<ReassignTaskResult>;

public record ReassignTaskResult(
    bool IsSuccess,
    bool Changed = false,
    string? AssignedTo = null,
    string? ErrorMessage = null);
