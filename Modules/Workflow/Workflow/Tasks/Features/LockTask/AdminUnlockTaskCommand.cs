namespace Workflow.Tasks.Features.LockTask;

public record AdminUnlockTaskCommand(Guid TaskId) : ICommand<AdminUnlockTaskResult>;

public record AdminUnlockTaskResult(bool IsSuccess, string? ErrorMessage = null);
