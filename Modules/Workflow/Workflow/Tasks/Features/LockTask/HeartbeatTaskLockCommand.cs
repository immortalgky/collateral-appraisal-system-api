namespace Workflow.Tasks.Features.LockTask;

public record HeartbeatTaskLockCommand(Guid TaskId) : ICommand<HeartbeatTaskLockResult>;

public record HeartbeatTaskLockResult(bool IsSuccess, string? ErrorMessage = null);
