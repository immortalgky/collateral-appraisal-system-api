namespace Workflow.Tasks.Features.SaveTaskDecisionDraft;

public record SaveTaskDecisionDraftCommand(
    Guid TaskId,
    string? DecisionTaken,
    string? Comment,
    string? ReasonCode,
    string? Assignee) : ICommand<SaveTaskDecisionDraftResult>;

public record SaveTaskDecisionDraftResult(
    bool IsSuccess,
    bool IsForbidden = false,
    string? ErrorMessage = null);
