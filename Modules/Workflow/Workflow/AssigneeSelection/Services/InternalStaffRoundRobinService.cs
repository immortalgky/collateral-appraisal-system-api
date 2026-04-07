using Workflow.Data.Repository;
using Workflow.Services.Groups;
using Workflow.Services.Hashing;

namespace Workflow.AssigneeSelection.Services;

public class InternalStaffRoundRobinService : IInternalStaffRoundRobinService
{
    private const string ActivityName = "InternalFollowupRouting";
    private const string GroupName = "IntAppraisalStaff";

    private readonly IUserGroupService _userGroupService;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IGroupHashService _groupHashService;
    private readonly ILogger<InternalStaffRoundRobinService> _logger;

    public InternalStaffRoundRobinService(
        IUserGroupService userGroupService,
        IAssignmentRepository assignmentRepository,
        IGroupHashService groupHashService,
        ILogger<InternalStaffRoundRobinService> logger)
    {
        _userGroupService = userGroupService;
        _assignmentRepository = assignmentRepository;
        _groupHashService = groupHashService;
        _logger = logger;
    }

    public async Task<StaffSelectionResult> SelectStaffAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await _userGroupService.GetUsersInGroupAsync(GroupName, cancellationToken);

            if (users.Count == 0)
            {
                _logger.LogWarning("No users found in {GroupName} for internal followup round-robin", GroupName);
                return StaffSelectionResult.Failure($"No users available in group '{GroupName}'");
            }

            var groupsHash = _groupHashService.GenerateGroupsHash([GroupName]);
            var groupsList = _groupHashService.GenerateGroupsList([GroupName]);

            await _assignmentRepository.SyncUsersForGroupCombinationAsync(
                ActivityName,
                groupsHash,
                groupsList,
                users,
                cancellationToken);

            var selectedId = await _assignmentRepository.SelectNextUserWithRoundResetAsync(
                ActivityName,
                groupsHash,
                cancellationToken);

            if (selectedId == null)
            {
                _logger.LogWarning("Round-robin returned no staff selection for internal followup");
                return StaffSelectionResult.Failure("Round-robin selection returned no result");
            }

            _logger.LogInformation(
                "Internal followup round-robin selected {UserId} from {TotalUsers} staff in {GroupName}",
                selectedId, users.Count, GroupName);

            return StaffSelectionResult.Success(selectedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select internal followup staff via round-robin");
            return StaffSelectionResult.Failure($"Staff selection failed: {ex.Message}");
        }
    }
}
