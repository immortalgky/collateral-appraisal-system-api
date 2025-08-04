namespace Assignment.AssigneeSelection.Strategies;

/// <summary>
/// Selects assignees based on current workload, choosing users with the lowest active task count
/// TODO: Implement read projections for better performance
/// </summary>
public class WorkloadBasedAssigneeSelector : IAssigneeSelector
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUserGroupService _userGroupService;
    private readonly ILogger<WorkloadBasedAssigneeSelector> _logger;

    public WorkloadBasedAssigneeSelector(
        IAssignmentRepository assignmentRepository,
        IUserGroupService userGroupService,
        ILogger<WorkloadBasedAssigneeSelector> logger)
    {
        _assignmentRepository = assignmentRepository;
        _userGroupService = userGroupService;
        _logger = logger;
    }

    public async Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var eligibleUsers = await GetEligibleUsersAsync(context, cancellationToken);

            if (!eligibleUsers.Any())
            {
                return AssigneeSelectionResult.Failure("No eligible users found for assignment");
            }

            var userWorkloads = await GetUserWorkloadsAsync(eligibleUsers, cancellationToken);
            var selectedUser = SelectUserWithLowestWorkload(userWorkloads);

            _logger.LogInformation(
                "Workload-based selector assigned user {UserId} for activity {ActivityName} (workload: {Workload})",
                selectedUser.UserId, context.ActivityName, selectedUser.Workload);

            return AssigneeSelectionResult.Success(selectedUser.UserId, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "WorkloadBased",
                ["SelectedUserWorkload"] = selectedUser.Workload,
                ["EligibleUserCount"] = eligibleUsers.Count,
                ["AllWorkloads"] = userWorkloads.ToDictionary(w => w.UserId, w => w.Workload)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during workload-based assignee selection");
            return AssigneeSelectionResult.Failure($"Selection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets eligible users based on the assignment context
    /// This implementation uses user groups - in production, you might also filter by:
    /// - User availability/schedule
    /// - Required skills/qualifications
    /// - Location constraints
    /// - Security clearance levels
    /// </summary>
    private async Task<List<string>> GetEligibleUsersAsync(AssignmentContext context,
        CancellationToken cancellationToken)
    {
        // Get users from specified groups if any
        if (context.UserGroups?.Any() == true)
        {
            var users = await _userGroupService.GetUsersInGroupsAsync(context.UserGroups, cancellationToken);
            _logger.LogDebug("Found {UserCount} eligible users from groups {Groups}",
                users.Count, string.Join(", ", context.UserGroups));
            return users;
        }

        // If no specific groups specified, use default assignment groups
        var defaultGroups = new List<string> { "Seniors", "Juniors" };
        var defaultUsers = await _userGroupService.GetUsersInGroupsAsync(defaultGroups, cancellationToken);

        _logger.LogDebug("Using default groups {Groups}, found {UserCount} eligible users",
            string.Join(", ", defaultGroups), defaultUsers.Count);

        return defaultUsers;
    }

    private async Task<List<UserWorkload>> GetUserWorkloadsAsync(List<string> userIds,
        CancellationToken cancellationToken)
    {
        var workloads = new List<UserWorkload>();

        foreach (var userId in userIds)
        {
            var activeTasks = await _assignmentRepository.GetActiveTaskCountForUserAsync(userId, cancellationToken);
            workloads.Add(new UserWorkload
            {
                UserId = userId,
                Workload = activeTasks
            });
        }

        return workloads;
    }

    private UserWorkload SelectUserWithLowestWorkload(List<UserWorkload> userWorkloads)
    {
        return userWorkloads
            .OrderBy(w => w.Workload)
            .ThenBy(w => w.UserId) // Secondary sort for deterministic results
            .First();
    }

    private class UserWorkload
    {
        public string UserId { get; set; } = string.Empty;
        public int Workload { get; set; }
    }
}