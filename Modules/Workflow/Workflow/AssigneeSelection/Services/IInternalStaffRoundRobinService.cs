namespace Workflow.AssigneeSelection.Services;

public interface IInternalStaffRoundRobinService
{
    Task<StaffSelectionResult> SelectStaffAsync(CancellationToken cancellationToken = default);
}

public record StaffSelectionResult(bool IsSuccess, string? UserId, string? ErrorMessage)
{
    public static StaffSelectionResult Success(string userId) =>
        new(true, userId, null);

    public static StaffSelectionResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
