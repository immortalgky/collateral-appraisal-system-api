using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Configuration;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Assigns tasks to supervisor/manager
/// </summary>
public class SupervisorAssigneeSelector : IAssigneeSelector
{
    private readonly ILogger<SupervisorAssigneeSelector> _logger;
    private readonly MockSupervisorOptions _mockOptions;

    public SupervisorAssigneeSelector(
        ILogger<SupervisorAssigneeSelector> logger,
        IOptions<MockSupervisorOptions> mockOptions)
    {
        _logger = logger;
        _mockOptions = mockOptions.Value;
    }

    public async Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Respect cancellation token at the beginning
            cancellationToken.ThrowIfCancellationRequested();

            var supervisor = GetSupervisorFromContext(context);

            if (string.IsNullOrEmpty(supervisor))
            {
                // Try to determine supervisor from user groups or other context
                supervisor = await DetermineSupervisorFromGroupsAsync(context, cancellationToken);
            }

            if (string.IsNullOrEmpty(supervisor))
            {
                return AssigneeSelectionResult.Failure(
                    "Supervisor assignment requires a supervisor to be specified or determinable from context");
            }

            var isEligible = await ValidateAssigneeEligibilityAsync(supervisor, context, cancellationToken);

            if (!isEligible)
            {
                return AssigneeSelectionResult.Failure(
                    $"Specified supervisor '{supervisor}' is not eligible for this assignment");
            }

            _logger.LogInformation("Supervisor selector assigned user {UserId} for activity {ActivityName}",
                supervisor, context.ActivityName);

            return AssigneeSelectionResult.Success(supervisor, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "Supervisor",
                ["SupervisorAssignment"] = true,
                ["SupervisorId"] = supervisor
            });
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during supervisor assignee selection");
            return AssigneeSelectionResult.Failure($"Selection failed: {ex.Message}");
        }
    }

    private string? GetSupervisorFromContext(AssignmentContext context)
    {
        if (context.Properties?.TryGetValue("SupervisorId", out var supervisor) == true)
        {
            return supervisor?.ToString();
        }

        return null;
    }

    private async Task<string?> DetermineSupervisorFromGroupsAsync(AssignmentContext context,
        CancellationToken cancellationToken)
    {
        // Respect cancellation token
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Yield(); // Make truly async

        // MOCK DATA: This will be replaced when UserManagement module is implemented
        // For now, using configurable supervisor assignments based on user groups

        if (context.UserGroups?.Any() != true)
        {
            _logger.LogWarning("No user groups provided in assignment context");
            return null;
        }

        // Validate input
        var validGroups = context.UserGroups.Where(g => !string.IsNullOrWhiteSpace(g)).ToList();
        if (!validGroups.Any())
        {
            _logger.LogWarning("All user groups in context are null or whitespace");
            return null;
        }

        foreach (var group in validGroups)
        {
            // Use invariant culture for consistent behavior
            var normalizedGroup = group.ToLower(CultureInfo.InvariantCulture);
            if (_mockOptions.SupervisorMappings.TryGetValue(normalizedGroup, out var supervisor))
            {
                _logger.LogInformation("Mock supervisor assignment: Group '{Group}' -> Supervisor '{Supervisor}'",
                    group, supervisor);
                return supervisor;
            }
        }

        // Default fallback supervisor for any unmatched groups
        if (!string.IsNullOrWhiteSpace(_mockOptions.DefaultSupervisor))
        {
            _logger.LogInformation("Using configured fallback supervisor '{Supervisor}' for groups: {Groups}",
                _mockOptions.DefaultSupervisor, string.Join(", ", validGroups));
            return _mockOptions.DefaultSupervisor;
        }

        _logger.LogWarning("No supervisor mapping found for groups: {Groups} and no default supervisor configured",
            string.Join(", ", validGroups));
        return null;
    }

    private async Task<bool> ValidateAssigneeEligibilityAsync(string assigneeId, AssignmentContext context,
        CancellationToken cancellationToken)
    {
        // Respect cancellation token
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Yield(); // Make truly async

        // MOCK DATA: Basic validation until UserManagement module is implemented
        // This will be replaced with real user validation service

        if (string.IsNullOrWhiteSpace(assigneeId))
        {
            _logger.LogWarning("Assignee ID is null or whitespace during validation");
            return false;
        }

        // Use configuration for mock validation
        var isValid = _mockOptions.ValidSupervisors.Contains(assigneeId);

        if (!isValid)
        {
            _logger.LogWarning(
                "Mock validation failed for supervisor '{SupervisorId}'. Valid supervisors: {ValidSupervisors}",
                assigneeId, string.Join(", ", _mockOptions.ValidSupervisors));
        }
        else
        {
            _logger.LogDebug("Mock validation succeeded for supervisor '{SupervisorId}'", assigneeId);
        }

        return isValid;
    }
}