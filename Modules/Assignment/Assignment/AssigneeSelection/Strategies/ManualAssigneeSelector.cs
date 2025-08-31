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
            var manualAssignee = context.UserCode;
            var manualAssigneeGroups = context.UserGroups;

            // Check for user assignment first (higher priority)
            if (!string.IsNullOrEmpty(manualAssignee))
            {
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
                    ["AssignmentType"] = "User",
                    ["ManuallySpecified"] = true,
                    ["AssignedBy"] = GetAssignerFromContext(context) ?? "system"
                });
            }

            // Check for group assignment
            if (manualAssigneeGroups.Any())
            {
                var isGroupValid = await ValidateGroupEligibilityAsync(manualAssigneeGroups, context, cancellationToken);

                if (!isGroupValid)
                {
                    return AssigneeSelectionResult.Failure(
                        $"Specified group '{manualAssigneeGroups}' is not eligible for this assignment");
                }

                _logger.LogInformation("Manual selector assigned to group {GroupId} for activity {ActivityName}",
                    manualAssigneeGroups, context.ActivityName);

                // For group assignment, return null assignee but include group info in metadata
                return AssigneeSelectionResult.Success(null!, new Dictionary<string, object>
                {
                    ["SelectionStrategy"] = "Manual",
                    ["AssignmentType"] = "Group",
                    ["AssignedGroup"] = manualAssigneeGroups,
                    ["ManuallySpecified"] = true,
                    ["AssignedBy"] = GetAssignerFromContext(context) ?? "system"
                });
            }

            return AssigneeSelectionResult.Failure(
                "Manual assignment requires either an explicit assignee or assignee group to be specified");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during manual assignee selection");
            return AssigneeSelectionResult.Failure($"Selection failed: {ex.Message}");
        }
    }

    private string? GetManualAssigneeFromContext(AssignmentContext context)
    {
        if (context.Properties?.TryGetValue("ManualAssignee", out var assignee) == true)
        {
            return assignee?.ToString();
        }

        return null;
    }

    private string? GetManualAssigneeGroupFromContext(AssignmentContext context)
    {
        if (context.Properties?.TryGetValue("ManualAssigneeGroup", out var group) == true)
        {
            return group?.ToString();
        }

        return null;
    }

    private string? GetAssignerFromContext(AssignmentContext context)
    {
        if (context.Properties?.TryGetValue("AssignedBy", out var assigner) == true)
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

    private async Task<bool> ValidateGroupEligibilityAsync(List<string> groupIds, AssignmentContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        // TODO: Implement actual group validation logic here.
        // Could be extended to check:
        // - Group exists and is active
        // - Group has eligible members
        // - Group has required permissions for the task
        // - Group is not overloaded with assignments

        return groupIds.Any();
    }
}