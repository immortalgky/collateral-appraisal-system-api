namespace Assignment.AssigneeSelection.Strategies;

/// <summary>
/// Represents a strategy for assigning tasks manually to a specific assignee based on explicit input.
/// </summary>
public class ManualAssigneeSelector : IAssigneeSelector
{
    private readonly ILogger<ManualAssigneeSelector> _logger;

    public ManualAssigneeSelector(ILogger<ManualAssigneeSelector> logger)
    {
        _logger = logger;
    }

    public async Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var manualAssignee = GetManualAssigneeFromContext(context);

            if (string.IsNullOrEmpty(manualAssignee))
            {
                return AssigneeSelectionResult.Failure(
                    "Manual assignment requires an explicit assignee to be specified");
            }

            var isEligible = await ValidateAssigneeEligibilityAsync(manualAssignee, context, cancellationToken);

            if (!isEligible)
            {
                return AssigneeSelectionResult.Failure(
                    $"Specified assignee '{manualAssignee}' is not eligible for this assignment");
            }

            _logger.LogInformation("Manual selector assigned user {UserId} for activity {ActivityName}",
                manualAssignee, context.ActivityName);

            return AssigneeSelectionResult.Success(manualAssignee, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "Manual",
                ["ManuallySpecified"] = true,
                ["AssignedBy"] = GetAssignerFromContext(context) ?? "system"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during manual assignee selection");
            return AssigneeSelectionResult.Failure($"Selection failed: {ex.Message}");
        }
    }

    private string? GetManualAssigneeFromContext(AssignmentContext context)
    {
        if (context.AdditionalCriteria?.TryGetValue("ManualAssignee", out var assignee) == true)
        {
            return assignee?.ToString();
        }

        return null;
    }

    private string? GetAssignerFromContext(AssignmentContext context)
    {
        if (context.AdditionalCriteria?.TryGetValue("AssignedBy", out var assigner) == true)
        {
            return assigner?.ToString();
        }

        return null;
    }

    private async Task<bool> ValidateAssigneeEligibilityAsync(string assigneeId, AssignmentContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        // TODO: Implement actual validation logic here.
        // Basic validation - could be extended to check:
        // - User exists and is active
        // - User has required role/permissions
        // - User is not overloaded
        // - User is available (not on leave, etc.)

        return !string.IsNullOrWhiteSpace(assigneeId);
    }
}