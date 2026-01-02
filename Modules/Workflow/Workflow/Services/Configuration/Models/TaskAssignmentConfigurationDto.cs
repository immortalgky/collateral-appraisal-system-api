namespace Workflow.Services.Configuration.Models;

/// <summary>
/// DTO for task assignment configuration
/// </summary>
public class TaskAssignmentConfigurationDto
{
    public Guid Id { get; set; }
    public string ActivityId { get; set; } = default!;
    public string? WorkflowDefinitionId { get; set; }
    public List<string> PrimaryStrategies { get; set; } = new();
    public List<string> RouteBackStrategies { get; set; } = new();
    public string? AdminPoolId { get; set; }
    public bool EscalateToAdminPool { get; set; } = true;
    public string? SpecificAssignee { get; set; }
    public string? AssigneeGroup { get; set; }
    public Dictionary<string, object>? AdditionalConfiguration { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = default!;
    public string UpdatedBy { get; set; } = default!;
}

/// <summary>
/// Request model for creating task assignment configuration
/// </summary>
public class CreateTaskAssignmentConfigurationRequest
{
    public string ActivityId { get; set; } = default!;
    public string? WorkflowDefinitionId { get; set; }
    public List<string> PrimaryStrategies { get; set; } = new();
    public List<string> RouteBackStrategies { get; set; } = new();
    public string? AdminPoolId { get; set; }
    public bool EscalateToAdminPool { get; set; } = true;
    public string? SpecificAssignee { get; set; }

    public string? AssigneeGroup { get; set; }

    // NOTE: SupervisorId and ReplacementUserId removed - now handled by UserManagement mock data
    public Dictionary<string, object>? AdditionalConfiguration { get; set; }
    public string CreatedBy { get; set; } = default!;
}

/// <summary>
/// Request model for updating task assignment configuration
/// </summary>
public class UpdateTaskAssignmentConfigurationRequest
{
    public List<string> PrimaryStrategies { get; set; } = new();
    public List<string> RouteBackStrategies { get; set; } = new();
    public string? AdminPoolId { get; set; }
    public bool EscalateToAdminPool { get; set; } = true;
    public string? SpecificAssignee { get; set; }

    public string? AssigneeGroup { get; set; }

    // NOTE: SupervisorId and ReplacementUserId removed - now handled by UserManagement mock data
    public Dictionary<string, object>? AdditionalConfiguration { get; set; }
    public string UpdatedBy { get; set; } = default!;
}