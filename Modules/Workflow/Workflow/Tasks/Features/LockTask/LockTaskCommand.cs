namespace Workflow.Tasks.Features.LockTask;

public record LockTaskCommand(Guid TaskId) : ICommand<LockTaskResult>;

public record LockTaskResult(bool IsSuccess, string? LockedBy = null, DateTime? LockedAt = null, string? ErrorMessage = null);
