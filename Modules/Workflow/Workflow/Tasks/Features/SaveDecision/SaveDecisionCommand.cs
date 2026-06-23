public record SaveDecisionCommand(
    Guid TaskId,
    string DecisionType,
    string AssignNextToType,
    string CommentDecision): ICommand<SaveDecisionResult>;

public record SaveDecisionResult(bool IsSuccess, string? ErrorMessage = null);