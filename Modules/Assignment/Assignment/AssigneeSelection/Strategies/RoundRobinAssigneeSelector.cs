namespace Assignment.AssigneeSelection.Strategies;

/// <summary>
/// Implements the round-robin strategy for selecting assignees in a balanced and deterministic manner.
/// </summary>
public class RoundRobinAssigneeSelector : IAssigneeSelector
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUserGroupService _userGroupService;
    private readonly IGroupHashService _groupHashService;
    private readonly ILogger<RoundRobinAssigneeSelector> _logger;

    public RoundRobinAssigneeSelector(
        IAssignmentRepository assignmentRepository,
        IUserGroupService userGroupService,
        IGroupHashService groupHashService,
        ILogger<RoundRobinAssigneeSelector> logger)
    {
        _assignmentRepository = assignmentRepository;
        _userGroupService = userGroupService;
        _groupHashService = groupHashService;
        _logger = logger;
    }

    public async Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            if (context.UserGroups == null || !context.UserGroups.Any())
            {
                return AssigneeSelectionResult.Failure("No user groups specified for round robin assignment");
            }

            // Generate group hash and list
            var groupsHash = _groupHashService.GenerateGroupsHash(context.UserGroups);
            var groupsList = _groupHashService.GenerateGroupsList(context.UserGroups);

            // Get eligible users from groups
            var eligibleUsers = await _userGroupService.GetUsersInGroupsAsync(context.UserGroups, cancellationToken);

            if (!eligibleUsers.Any())
            {
                return AssigneeSelectionResult.Failure("No eligible users found in specified groups");
            }

            // Sync users for this activity and group combination
            await _assignmentRepository.SyncUsersForGroupCombinationAsync(
                context.ActivityName,
                groupsHash,
                groupsList,
                eligibleUsers,
                cancellationToken);

            // Select the next user with automatic round reset
            var selectedUser = await _assignmentRepository.SelectNextUserWithRoundResetAsync(
                context.ActivityName,
                groupsHash,
                cancellationToken);

            if (selectedUser == null)
            {
                return AssigneeSelectionResult.Failure("No active users available for assignment");
            }

            _logger.LogInformation(
                "Round robin selector assigned user {UserId} for activity {ActivityName} with groups {Groups}",
                selectedUser, context.ActivityName, groupsList);

            return AssigneeSelectionResult.Success(selectedUser, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "RoundRobin",
                ["GroupsHash"] = groupsHash,
                ["GroupsList"] = groupsList,
                ["EligibleUserCount"] = eligibleUsers.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during round robin assignee selection for activity {ActivityName}",
                context.ActivityName);
            return AssigneeSelectionResult.Failure($"Selection failed: {ex.Message}");
        }
    }
}