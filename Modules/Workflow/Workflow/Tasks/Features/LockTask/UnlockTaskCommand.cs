namespace Workflow.Tasks.Features.LockTask;

public record UnlockTaskCommand(Guid TaskId) : ICommand<UnlockTaskResult>;

public record UnlockTaskResult(bool IsSuccess, string? ErrorMessage = null);
