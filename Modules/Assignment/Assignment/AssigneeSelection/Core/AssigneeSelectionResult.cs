namespace Assignment.AssigneeSelection.Core;

public class AssigneeSelectionResult
{
    public string? AssigneeId { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    public static AssigneeSelectionResult Success(string assigneeId, Dictionary<string, object>? metadata = null)
    {
        return new AssigneeSelectionResult
        {
            AssigneeId = assigneeId,
            IsSuccess = true,
            Metadata = metadata
        };
    }

    public static AssigneeSelectionResult Failure(string errorMessage)
    {
        return new AssigneeSelectionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}