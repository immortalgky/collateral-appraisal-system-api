using Shared.DDD;

namespace Workflow.Data.Entities;

/// <summary>
/// External configuration for task assignment strategies and fallback options
/// </summary>
public class TaskAssignmentConfiguration : Entity<Guid>
{
    /// <summary>
    /// Activity identifier that this configuration applies to
    /// </summary>
    public string ActivityId { get; private set; } = default!;

    /// <summary>
    /// Workflow definition identifier (optional - for more specific scoping)
    /// </summary>
    public string? WorkflowDefinitionId { get; private set; }

    /// <summary>
    /// Primary assignment strategies in order of preference (JSON array)
    /// </summary>
    public string PrimaryStrategies { get; private set; } = default!;

    /// <summary>
    /// Route-back assignment strategies in order of preference (JSON array)
    /// </summary>
    public string RouteBackStrategies { get; private set; } = default!;

    /// <summary>
    /// Admin pool ID for fallback when all assignment strategies fail
    /// </summary>
    public string? AdminPoolId { get; private set; }

    /// <summary>
    /// Whether to escalate to admin pool when no assignee can be found
    /// </summary>
    public bool EscalateToAdminPool { get; private set; }

    /// <summary>
    /// Specific assignee for manual assignments
    /// </summary>
    public string? SpecificAssignee { get; private set; }

    /// <summary>
    /// Group ID for group assignments
    /// </summary>
    public string? AssigneeGroup { get; private set; }

    // NOTE: SupervisorId and ReplacementUserId removed - now handled by UserManagement mock data

    /// <summary>
    /// Additional configuration as JSON
    /// </summary>
    public string? AdditionalConfiguration { get; private set; }

    /// <summary>
    /// Whether this configuration is active
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Created by user
    /// </summary>
    public new string CreatedBy { get; private set; } = default!;

    /// <summary>
    /// Last updated by user
    /// </summary>
    public new string UpdatedBy { get; private set; } = default!;

    private TaskAssignmentConfiguration()
    {
        // For EF Core
    }

    public static TaskAssignmentConfiguration Create(
        string activityId,
        string primaryStrategies,
        string routeBackStrategies,
        string createdBy,
        string? workflowDefinitionId = null,
        string? specificAssignee = null,
        string? assigneeGroup = null,
        string? adminPoolId = null,
        bool escalateToAdminPool = true,
        string? additionalConfiguration = null)
    {
        return new TaskAssignmentConfiguration
        {
            ActivityId = activityId,
            WorkflowDefinitionId = workflowDefinitionId,
            PrimaryStrategies = primaryStrategies,
            RouteBackStrategies = routeBackStrategies,
            AdminPoolId = adminPoolId,
            EscalateToAdminPool = escalateToAdminPool,
            SpecificAssignee = specificAssignee,
            AssigneeGroup = assigneeGroup,
            AdditionalConfiguration = additionalConfiguration,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };
    }

    public void Update(
        string primaryStrategies,
        string routeBackStrategies,
        string updatedBy,
        string? specificAssignee = null,
        string? assigneeGroup = null,
        string? adminPoolId = null,
        bool? escalateToAdminPool = null,
        string? additionalConfiguration = null)
    {
        PrimaryStrategies = primaryStrategies;
        RouteBackStrategies = routeBackStrategies;
        SpecificAssignee = specificAssignee;
        AssigneeGroup = assigneeGroup;
        
        if (adminPoolId != null)
            AdminPoolId = adminPoolId;
        
        if (escalateToAdminPool.HasValue)
            EscalateToAdminPool = escalateToAdminPool.Value;
            
        AdditionalConfiguration = additionalConfiguration;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Activate(string updatedBy)
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Deactivate(string updatedBy)
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}