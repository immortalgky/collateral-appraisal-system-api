namespace Workflow.Tasks.Features.StartTask;

public record StartTaskCommand(Guid TaskId) : ICommand<StartTaskResult>;

public record StartTaskResult(bool IsSuccess, string? ErrorMessage = null);
