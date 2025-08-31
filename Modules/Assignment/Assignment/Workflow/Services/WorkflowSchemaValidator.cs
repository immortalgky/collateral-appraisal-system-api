using Assignment.Workflow.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Assignment.Workflow.Services;

/// <summary>
/// Service responsible for validating and deserializing workflow schemas
/// Handles infrastructure concerns related to schema validation and serialization
/// </summary>
public class WorkflowSchemaValidator : IWorkflowSchemaValidator
{
    private readonly ILogger<WorkflowSchemaValidator> _logger;

    public WorkflowSchemaValidator(ILogger<WorkflowSchemaValidator> logger)
    {
        _logger = logger;
    }

    public WorkflowSchema? DeserializeWorkflowSchemaSecurely(string jsonDefinition, Guid workflowDefinitionId)
    {
        // Validate JSON size to prevent memory exhaustion
        const int maxJsonSize = 1024 * 1024; // 1MB limit
        if (jsonDefinition.Length > maxJsonSize)
        {
            throw new InvalidOperationException(
                $"Workflow definition JSON exceeds maximum size limit for: {workflowDefinitionId}");
        }

        // Configure secure JSON options
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = 32, // Prevent stack overflow from deeply nested objects
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Disable dangerous features
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        try
        {
            var workflowSchema = JsonSerializer.Deserialize<WorkflowSchema>(jsonDefinition, jsonOptions);

            // Additional validation
            if (workflowSchema != null)
            {
                ValidateWorkflowSchemaStructure(workflowSchema);
            }

            return workflowSchema;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON format in workflow definition {WorkflowDefinitionId}", workflowDefinitionId);
            throw new InvalidOperationException(
                $"Invalid JSON format in workflow definition {workflowDefinitionId}: {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported JSON structure in workflow definition {WorkflowDefinitionId}", workflowDefinitionId);
            throw new InvalidOperationException(
                $"Unsupported JSON structure in workflow definition {workflowDefinitionId}: {ex.Message}", ex);
        }
    }

    public void ValidateWorkflowSchemaStructure(WorkflowSchema workflowSchema)
    {
        // Validate essential structure
        if (string.IsNullOrWhiteSpace(workflowSchema.Name))
        {
            throw new InvalidOperationException("Workflow schema must have a valid name");
        }

        if (workflowSchema.Activities == null || !workflowSchema.Activities.Any())
        {
            throw new InvalidOperationException("Workflow schema must have at least one activity");
        }

        // Validate activity count to prevent resource exhaustion
        const int maxActivities = 1000;
        if (workflowSchema.Activities.Count > maxActivities)
        {
            throw new InvalidOperationException($"Workflow schema exceeds maximum activity limit of {maxActivities}");
        }

        // Validate transitions count
        const int maxTransitions = 2000;
        if (workflowSchema.Transitions?.Count > maxTransitions)
        {
            throw new InvalidOperationException(
                $"Workflow schema exceeds maximum transition limit of {maxTransitions}");
        }

        // Validate activity IDs are unique and safe
        var activityIds = new HashSet<string>();
        foreach (var activity in workflowSchema.Activities)
        {
            if (string.IsNullOrWhiteSpace(activity.Id))
            {
                throw new InvalidOperationException("All activities must have valid IDs");
            }

            if (!activityIds.Add(activity.Id))
            {
                throw new InvalidOperationException($"Duplicate activity ID found: {activity.Id}");
            }

            // Validate activity ID format (alphanumeric, underscore, hyphen only)
            if (!Regex.IsMatch(activity.Id, @"^[a-zA-Z0-9_-]+$"))
            {
                throw new InvalidOperationException($"Activity ID contains invalid characters: {activity.Id}");
            }
        }

        _logger.LogDebug("Workflow schema validation completed successfully for '{WorkflowName}'", workflowSchema.Name);
    }
}