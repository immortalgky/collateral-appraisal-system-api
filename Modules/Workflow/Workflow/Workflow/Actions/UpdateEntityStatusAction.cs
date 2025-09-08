using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Actions;

/// <summary>
/// Action that updates entity status during workflow execution
/// Useful for updating request status, document status, or other business entities
/// </summary>
public class UpdateEntityStatusAction : WorkflowActionBase
{
    private readonly IEntityStatusService _entityStatusService;

    public UpdateEntityStatusAction(IEntityStatusService entityStatusService, ILogger<UpdateEntityStatusAction> logger) : base(logger)
    {
        _entityStatusService = entityStatusService;
    }

    public override string ActionType => "UpdateEntityStatus";
    public override string Name => "Update Entity Status";
    public override string Description => "Updates the status of business entities during workflow execution";

    protected override async Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var entityType = GetParameter<string>(actionParameters, "entityType");
        var entityId = GetParameter<string>(actionParameters, "entityId");
        var newStatus = GetParameter<string>(actionParameters, "newStatus");
        var reason = GetParameter<string>(actionParameters, "reason", "");
        var validateTransition = GetParameter<bool>(actionParameters, "validateTransition", true);
        var includeAuditTrail = GetParameter<bool>(actionParameters, "includeAuditTrail", true);

        Logger.LogDebug("Updating {EntityType} {EntityId} status to '{NewStatus}' for activity {ActivityId}",
            entityType, entityId, newStatus, context.ActivityId);

        try
        {
            // Resolve variable expressions in parameters
            var resolvedEntityId = ResolveVariableExpressions(entityId, context);
            var resolvedNewStatus = ResolveVariableExpressions(newStatus, context);
            var resolvedReason = ResolveVariableExpressions(reason, context);

            // Get current status if validation is enabled
            string? currentStatus = null;
            if (validateTransition)
            {
                currentStatus = await _entityStatusService.GetEntityStatusAsync(entityType, resolvedEntityId, cancellationToken);
                Logger.LogDebug("Current status of {EntityType} {EntityId}: {CurrentStatus}",
                    entityType, resolvedEntityId, currentStatus);
            }

            // Prepare status update request
            var updateRequest = new EntityStatusUpdateRequest
            {
                EntityType = entityType,
                EntityId = resolvedEntityId,
                NewStatus = resolvedNewStatus,
                PreviousStatus = currentStatus,
                Reason = resolvedReason,
                UpdatedBy = context.CurrentAssignee,
                WorkflowContext = new Dictionary<string, object>
                {
                    ["workflowInstanceId"] = context.WorkflowInstanceId,
                    ["activityId"] = context.ActivityId,
                    ["timestamp"] = DateTime.UtcNow
                },
                ValidateTransition = validateTransition,
                IncludeAuditTrail = includeAuditTrail
            };

            // Execute the status update
            var updateResult = await _entityStatusService.UpdateEntityStatusAsync(updateRequest, cancellationToken);

            if (!updateResult.IsSuccess)
            {
                var errorMessage = $"Failed to update {entityType} {resolvedEntityId} status: {updateResult.ErrorMessage}";
                Logger.LogError("Status update failed for {EntityType} {EntityId}: {Error}",
                    entityType, resolvedEntityId, updateResult.ErrorMessage);
                
                return ActionExecutionResult.Failed(errorMessage);
            }

            var resultMessage = currentStatus != null 
                ? $"Updated {entityType} {resolvedEntityId} status from '{currentStatus}' to '{resolvedNewStatus}'"
                : $"Set {entityType} {resolvedEntityId} status to '{resolvedNewStatus}'";

            var outputData = new Dictionary<string, object>
            {
                ["entityType"] = entityType,
                ["entityId"] = resolvedEntityId,
                ["newStatus"] = resolvedNewStatus,
                ["previousStatus"] = currentStatus ?? "",
                ["reason"] = resolvedReason,
                ["timestamp"] = DateTime.UtcNow,
                ["updatedBy"] = context.CurrentAssignee ?? "",
                ["transitionValidated"] = validateTransition
            };

            // Add any additional data from the update result
            if (updateResult.AdditionalData.Any())
            {
                foreach (var kvp in updateResult.AdditionalData)
                {
                    outputData[$"result_{kvp.Key}"] = kvp.Value;
                }
            }

            Logger.LogInformation("Successfully updated {EntityType} {EntityId} status to '{NewStatus}' for activity {ActivityId}",
                entityType, resolvedEntityId, resolvedNewStatus, context.ActivityId);

            return ActionExecutionResult.Success(resultMessage, outputData);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error updating {entityType} {entityId} status: {ex.Message}";
            Logger.LogError(ex, "Error updating entity status for activity {ActivityId}",
                context.ActivityId);
            
            return ActionExecutionResult.Failed(errorMessage);
        }
    }

    public override Task<ActionValidationResult> ValidateAsync(
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate required parameters
        ValidateRequiredParameter(actionParameters, "entityType", errors);
        ValidateRequiredParameter(actionParameters, "entityId", errors);
        ValidateRequiredParameter(actionParameters, "newStatus", errors);

        var entityType = GetParameter<string>(actionParameters, "entityType");
        var entityId = GetParameter<string>(actionParameters, "entityId");
        var newStatus = GetParameter<string>(actionParameters, "newStatus");

        // Validate entity type format
        if (!string.IsNullOrEmpty(entityType))
        {
            if (entityType.Contains(" ") || !char.IsUpper(entityType[0]))
            {
                warnings.Add($"Entity type '{entityType}' should follow PascalCase naming convention (e.g., 'AppraisalRequest')");
            }
        }

        // Validate entity ID format - check if it contains variable expressions or is a valid ID
        if (!string.IsNullOrEmpty(entityId))
        {
            if (!entityId.Contains("${") && !Guid.TryParse(entityId, out _) && !int.TryParse(entityId, out _))
            {
                warnings.Add($"Entity ID '{entityId}' does not appear to be a valid GUID, integer, or variable expression");
            }
        }

        // Validate status value format
        if (!string.IsNullOrEmpty(newStatus))
        {
            if (newStatus.Contains(" ") && !newStatus.Contains("${"))
            {
                warnings.Add($"Status value '{newStatus}' contains spaces. Consider using camelCase or PascalCase format");
            }
        }

        return Task.FromResult(errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings));
    }
}

/// <summary>
/// Interface for entity status service - to be implemented by the application's entity management system
/// </summary>
public interface IEntityStatusService
{
    /// <summary>
    /// Gets the current status of an entity
    /// </summary>
    /// <param name="entityType">Type of entity</param>
    /// <param name="entityId">Entity identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current status of the entity</returns>
    Task<string?> GetEntityStatusAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of an entity
    /// </summary>
    /// <param name="updateRequest">Status update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the status update</returns>
    Task<EntityStatusUpdateResult> UpdateEntityStatusAsync(EntityStatusUpdateRequest updateRequest, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request model for entity status updates
/// </summary>
public class EntityStatusUpdateRequest
{
    public string EntityType { get; init; } = default!;
    public string EntityId { get; init; } = default!;
    public string NewStatus { get; init; } = default!;
    public string? PreviousStatus { get; init; }
    public string? Reason { get; init; }
    public string? UpdatedBy { get; init; }
    public Dictionary<string, object> WorkflowContext { get; init; } = new();
    public bool ValidateTransition { get; init; } = true;
    public bool IncludeAuditTrail { get; init; } = true;
}

/// <summary>
/// Result of an entity status update operation
/// </summary>
public class EntityStatusUpdateResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? PreviousStatus { get; init; }
    public string NewStatus { get; init; } = default!;
    public DateTime UpdatedAt { get; init; }
    public Dictionary<string, object> AdditionalData { get; init; } = new();
    
    public static EntityStatusUpdateResult Success(
        string newStatus,
        string? previousStatus = null,
        Dictionary<string, object>? additionalData = null)
        => new()
        {
            IsSuccess = true,
            NewStatus = newStatus,
            PreviousStatus = previousStatus,
            UpdatedAt = DateTime.UtcNow,
            AdditionalData = additionalData ?? new Dictionary<string, object>()
        };
    
    public static EntityStatusUpdateResult Failed(string errorMessage)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            NewStatus = "",
            UpdatedAt = DateTime.UtcNow
        };
}