namespace Workflow.Tasks.Features.ClaimTask;

public record ClaimTaskCommand(Guid TaskId) : ICommand<ClaimTaskResult>;

public record ClaimTaskResult(bool IsSuccess, string? AssignedTo = null, string? ErrorMessage = null);
