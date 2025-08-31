namespace Assignment.AssigneeSelection.Services;

/// <summary>
/// Custom assignment service that assigns tasks based on client preferences
/// Checks if a client has a preferred reviewer and assigns accordingly
/// </summary>
public class ClientPreferenceAssignmentService : ICustomAssignmentService
{
    private readonly ILogger<ClientPreferenceAssignmentService> _logger;
    
    // Mock client preferences - in real implementation this would come from a database or external service
    private readonly Dictionary<string, string> _clientPreferences = new()
    {
        ["CLIENT001"] = "john.reviewer@company.com",
        ["CLIENT002"] = "jane.expert@company.com",
        ["CLIENT003"] = "mike.senior@company.com",
        ["VIP_CLIENT_001"] = "senior.team@company.com", // Group preference
        ["VIP_CLIENT_002"] = "premium.reviewers@company.com"
    };

    public ClientPreferenceAssignmentService(ILogger<ClientPreferenceAssignmentService> logger)
    {
        _logger = logger;
    }

    public async Task<CustomAssignmentResult> GetAssignmentContextAsync(
        string workflowInstanceId, 
        string activityId, 
        Dictionary<string, object> workflowVariables, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract client identifier from workflow variables
            var clientId = GetClientId(workflowVariables);
            
            if (string.IsNullOrEmpty(clientId))
            {
                _logger.LogDebug("No client ID found in workflow variables for instance {WorkflowInstanceId}", 
                    workflowInstanceId);
                return CustomAssignmentResult.NoCustomAssignment("No client ID available");
            }

            _logger.LogDebug("Checking assignment preferences for client {ClientId} in workflow {WorkflowInstanceId}", 
                clientId, workflowInstanceId);

            // Check if client has a preference
            if (!_clientPreferences.TryGetValue(clientId, out var preferredAssignee))
            {
                _logger.LogDebug("No assignment preference found for client {ClientId}", clientId);
                return CustomAssignmentResult.NoCustomAssignment($"No preference configured for client {clientId}");
            }

            // Determine if preference is for a user or group
            if (preferredAssignee.Contains("@"))
            {
                // Email format suggests individual user
                _logger.LogInformation("Assigning to preferred reviewer {PreferredReviewer} for client {ClientId}", 
                    preferredAssignee, clientId);

                return CustomAssignmentResult.ForAssignee(
                    preferredAssignee, 
                    $"Client {clientId} requested specific reviewer {preferredAssignee}");
            }
            else
            {
                // Non-email format suggests group
                _logger.LogInformation("Assigning to preferred group {PreferredGroup} for client {ClientId}", 
                    preferredAssignee, clientId);

                return CustomAssignmentResult.ForGroup(
                    preferredAssignee, 
                    $"Client {clientId} requested assignment to group {preferredAssignee}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in client preference assignment service for workflow {WorkflowInstanceId}, activity {ActivityId}", 
                workflowInstanceId, activityId);
            
            // Return no custom assignment on error to allow fallback to standard logic
            return CustomAssignmentResult.NoCustomAssignment($"Error checking client preferences: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts client ID from workflow variables using multiple possible keys
    /// </summary>
    /// <param name="workflowVariables">Workflow variables dictionary</param>
    /// <returns>Client ID if found, null otherwise</returns>
    private string? GetClientId(Dictionary<string, object> workflowVariables)
    {
        // Try multiple possible keys for client ID
        var possibleKeys = new[] { "clientId", "ClientId", "client_id", "customerId", "applicantId" };
        
        foreach (var key in possibleKeys)
        {
            if (workflowVariables.TryGetValue(key, out var value) && value != null)
            {
                var clientId = value.ToString();
                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    _logger.LogDebug("Found client ID '{ClientId}' using key '{Key}'", clientId, key);
                    return clientId;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Adds or updates a client preference (for testing or admin functionality)
    /// In production, this would interact with a database or external service
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <param name="preferredAssignee">The preferred reviewer (user email or group name)</param>
    public void SetClientPreference(string clientId, string preferredAssignee)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));
            
        if (string.IsNullOrWhiteSpace(preferredAssignee))
            throw new ArgumentException("Preferred assignee cannot be null or empty", nameof(preferredAssignee));

        _clientPreferences[clientId] = preferredAssignee;
        _logger.LogInformation("Updated client preference: {ClientId} -> {PreferredAssignee}", 
            clientId, preferredAssignee);
    }

    /// <summary>
    /// Gets all configured client preferences (for debugging/admin purposes)
    /// </summary>
    /// <returns>Dictionary of client preferences</returns>
    public IReadOnlyDictionary<string, string> GetAllPreferences()
    {
        return _clientPreferences.AsReadOnly();
    }
}