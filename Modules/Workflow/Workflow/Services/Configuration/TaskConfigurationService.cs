using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Services.Configuration.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Workflow.Services.Configuration;

/// <summary>
/// Implementation of task configuration service for managing external assignment configurations
/// </summary>
public class TaskConfigurationService : ITaskConfigurationService
{
    private readonly WorkflowDbContext _context;
    private readonly ILogger<TaskConfigurationService> _logger;

    public TaskConfigurationService(
        WorkflowDbContext context,
        ILogger<TaskConfigurationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TaskAssignmentConfigurationDto?> GetConfigurationAsync(
        string activityId,
        string? workflowDefinitionId = null,
        string? bankingSegment = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Empty/whitespace segment (the workflow variable is defaulted to "") is treated as "no segment".
            var segment = string.IsNullOrWhiteSpace(bankingSegment) ? null : bankingSegment;
            var workflow = string.IsNullOrEmpty(workflowDefinitionId) ? null : workflowDefinitionId;

            // Pull the small candidate set whose scopes are compatible with this activity, then rank in memory.
            var candidates = await _context.TaskAssignmentConfigurations
                .Where(c => c.ActivityId == activityId && c.IsActive)
                .Where(c => c.WorkflowDefinitionId == null || c.WorkflowDefinitionId == workflow)
                .Where(c => c.BankingSegment == null || c.BankingSegment == segment)
                .ToListAsync(cancellationToken);

            if (candidates.Count == 0)
                return null;

            // Most-specific wins. WorkflowDefinitionId specificity ranks above BankingSegment specificity.
            //   tier 4: wf match (+2) & segment match (+1)
            //   tier 3: wf match (+2)
            //   tier 2: segment match (+1)
            //   tier 1: wildcard (0)
            var best = candidates
                .OrderByDescending(c =>
                    (c.WorkflowDefinitionId != null ? 2 : 0) +
                    (c.BankingSegment != null ? 1 : 0))
                .First();

            return MapToDto(best);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task assignment configuration for activity {ActivityId}", activityId);
            return null;
        }
    }

    public async Task<TaskAssignmentConfigurationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.TaskAssignmentConfigurations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<List<TaskAssignmentConfigurationDto>> ListConfigurationsAsync(
        string? activityId = null,
        string? bankingSegment = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TaskAssignmentConfigurations.AsQueryable();

        if (!string.IsNullOrWhiteSpace(activityId))
            query = query.Where(c => c.ActivityId == activityId);

        if (!string.IsNullOrWhiteSpace(bankingSegment))
            query = query.Where(c => c.BankingSegment == bankingSegment);

        var entities = await query
            .OrderBy(c => c.ActivityId)
            .ThenBy(c => c.BankingSegment)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<TaskAssignmentConfigurationDto> CreateConfigurationAsync(
        CreateTaskAssignmentConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = TaskAssignmentConfiguration.Create(
                request.ActivityId,
                JsonSerializer.Serialize(request.PrimaryStrategies),
                JsonSerializer.Serialize(request.RouteBackStrategies),
                request.CreatedBy,
                request.WorkflowDefinitionId,
                request.SpecificAssignee,
                request.AssigneeGroup,
                request.BankingSegment,
                request.AdminPoolId,
                request.EscalateToAdminPool,
                request.AdditionalConfiguration != null ? JsonSerializer.Serialize(request.AdditionalConfiguration) : null,
                request.IsActive);

            _context.TaskAssignmentConfigurations.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created task assignment configuration {ConfigId} for activity {ActivityId}",
                entity.Id, request.ActivityId);

            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task assignment configuration for activity {ActivityId}", request.ActivityId);
            throw;
        }
    }

    public async Task<TaskAssignmentConfigurationDto> UpdateConfigurationAsync(
        Guid id,
        UpdateTaskAssignmentConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _context.TaskAssignmentConfigurations
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (entity == null)
            {
                throw new InvalidOperationException($"Task assignment configuration with ID {id} not found");
            }

            entity.Update(
                JsonSerializer.Serialize(request.PrimaryStrategies),
                JsonSerializer.Serialize(request.RouteBackStrategies),
                request.UpdatedBy,
                request.SpecificAssignee,
                request.AssigneeGroup,
                request.BankingSegment,
                request.AdminPoolId,
                request.EscalateToAdminPool,
                request.AdditionalConfiguration != null ? JsonSerializer.Serialize(request.AdditionalConfiguration) : null,
                request.IsActive);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated task assignment configuration {ConfigId}", id);

            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task assignment configuration {ConfigId}", id);
            throw;
        }
    }

    public async Task DeleteConfigurationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _context.TaskAssignmentConfigurations
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (entity == null)
            {
                throw new InvalidOperationException($"Task assignment configuration with ID {id} not found");
            }

            _context.TaskAssignmentConfigurations.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted task assignment configuration {ConfigId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task assignment configuration {ConfigId}", id);
            throw;
        }
    }

    public async Task<List<TaskAssignmentConfigurationDto>> GetConfigurationsByWorkflowAsync(
        string workflowDefinitionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _context.TaskAssignmentConfigurations
                .Where(c => c.WorkflowDefinitionId == workflowDefinitionId && c.IsActive)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task assignment configurations for workflow {WorkflowDefinitionId}", workflowDefinitionId);
            throw;
        }
    }

    private TaskAssignmentConfigurationDto MapToDto(TaskAssignmentConfiguration entity)
    {
        return new TaskAssignmentConfigurationDto
        {
            Id = entity.Id,
            ActivityId = entity.ActivityId,
            WorkflowDefinitionId = entity.WorkflowDefinitionId,
            PrimaryStrategies = DeserializeStrategies(entity.PrimaryStrategies),
            RouteBackStrategies = DeserializeStrategies(entity.RouteBackStrategies),
            AdminPoolId = entity.AdminPoolId,
            EscalateToAdminPool = entity.EscalateToAdminPool,
            SpecificAssignee = entity.SpecificAssignee,
            AssigneeGroup = entity.AssigneeGroup,
            BankingSegment = entity.BankingSegment,
            // NOTE: SupervisorId and ReplacementUserId removed - now handled by UserManagement mock data
            AdditionalConfiguration = DeserializeAdditionalConfiguration(entity.AdditionalConfiguration),
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy
        };
    }

    private List<string> DeserializeStrategies(string strategiesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(strategiesJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to deserialize strategies JSON: {Json}", strategiesJson);
            return new List<string>();
        }
    }

    private Dictionary<string, object>? DeserializeAdditionalConfiguration(string? configJson)
    {
        if (string.IsNullOrEmpty(configJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(configJson);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to deserialize additional configuration JSON: {Json}", configJson);
            return null;
        }
    }
}