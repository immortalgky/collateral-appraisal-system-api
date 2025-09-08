namespace Workflow.AssigneeSelection.Configuration;

/// <summary>
/// Configuration options for mock supervisor assignments
/// This will be removed when UserManagement module is implemented
/// </summary>
public class MockSupervisorOptions
{
    public const string SectionName = "MockSupervisor";

    /// <summary>
    /// Group to supervisor mappings
    /// </summary>
    public Dictionary<string, string> SupervisorMappings { get; set; } = new()
    {
        ["appraisers"] = "supervisor-001",
        ["reviewers"] = "supervisor-002",
        ["underwriters"] = "supervisor-003",
        ["analysts"] = "supervisor-001",
        ["managers"] = "senior-manager-001"
    };

    /// <summary>
    /// Valid supervisor IDs for validation
    /// </summary>
    public HashSet<string> ValidSupervisors { get; set; } = new()
    {
        "supervisor-001",
        "supervisor-002",
        "supervisor-003",
        "senior-manager-001",
        "default-supervisor-001"
    };

    /// <summary>
    /// Default supervisor when no mapping is found
    /// </summary>
    public string DefaultSupervisor { get; set; } = "default-supervisor-001";

    /// <summary>
    /// Validate configuration integrity
    /// </summary>
    public void Validate()
    {
        if (SupervisorMappings.Any(kvp => string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value)))
        {
            throw new InvalidOperationException("SupervisorMappings contains null or empty keys/values");
        }

        if (!string.IsNullOrWhiteSpace(DefaultSupervisor) && !ValidSupervisors.Contains(DefaultSupervisor))
        {
            throw new InvalidOperationException($"DefaultSupervisor '{DefaultSupervisor}' is not in ValidSupervisors collection");
        }

        foreach (var supervisor in SupervisorMappings.Values.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            if (!ValidSupervisors.Contains(supervisor))
            {
                throw new InvalidOperationException($"Supervisor '{supervisor}' in mappings is not in ValidSupervisors collection");
            }
        }
    }
}