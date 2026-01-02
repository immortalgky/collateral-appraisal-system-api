using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Config;
using System.Text.Json;
using System.Text.Json.Serialization;
using Workflow.Data;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Data;
using Dapper;

namespace Workflow.Workflow.Services;

/// <summary>
/// Handles all workflow-related data persistence operations
/// Service Layer responsibility - manages transactions, data validation, and repository coordination
/// </summary>
public class WorkflowPersistenceService : IWorkflowPersistenceService
{
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowActivityExecutionRepository _executionRepository;
    private readonly WorkflowDbContext _dbContext;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly ILogger<WorkflowPersistenceService> _logger;

    public WorkflowPersistenceService(
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowActivityExecutionRepository executionRepository,
        WorkflowDbContext dbContext,
        ISqlConnectionFactory sqlConnectionFactory,
        ILogger<WorkflowPersistenceService> logger)
    {
        _definitionRepository = definitionRepository;
        _instanceRepository = instanceRepository;
        _executionRepository = executionRepository;
        _dbContext = dbContext;
        _sqlConnectionFactory = sqlConnectionFactory;
        _logger = logger;
    }

    public async Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _definitionRepository.GetByIdAsync(definitionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve workflow definition {DefinitionId}", definitionId);
            throw;
        }
    }

    public async Task<WorkflowSchema?> GetWorkflowSchemaAsync(Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var definition = await GetWorkflowDefinitionAsync(definitionId, cancellationToken);
        if (definition == null) return null;

        try
        {
            return DeserializeWorkflowSchemaSecurely(definition.JsonDefinition, definitionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize workflow schema for definition {DefinitionId}", definitionId);
            return null;
        }
    }

    public async Task SaveWorkflowInstanceAsync(WorkflowInstance workflowInstance,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _instanceRepository.AddAsync(workflowInstance, cancellationToken);
            await _instanceRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workflow instance {InstanceId}", workflowInstance.Id);
            throw;
        }
    }

    public async Task UpdateWorkflowInstanceAsync(WorkflowInstance workflowInstance,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Manually mark as modified since EF Core does not track JSON column properly
            await _instanceRepository.UpdateAsync(workflowInstance, cancellationToken);
            await _instanceRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update workflow instance {InstanceId}", workflowInstance.Id);
            throw;
        }
    }

    public async Task<WorkflowInstance?> GetWorkflowInstanceAsync(Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _instanceRepository.GetByIdAsync(instanceId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve workflow instance {InstanceId}", instanceId);
            throw;
        }
    }

    public async Task<WorkflowInstance?> GetWorkflowInstanceWithExecutionsAsync(Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _instanceRepository.GetWithExecutionsAsync(instanceId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve workflow instance with executions {InstanceId}", instanceId);
            throw;
        }
    }

    public async Task<WorkflowInstance?> GetWorkflowInstanceByCorrelationId(string correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _instanceRepository.GetByCorrelationId(correlationId, cancellationToken);
    }

    public async Task<WorkflowActivityExecution> CreateActivityExecutionAsync(
        Guid workflowInstanceId,
        string activityId,
        string activityName,
        string activityType,
        string? assignedTo = null,
        Dictionary<string, object>? inputData = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var execution = WorkflowActivityExecution.Create(
                workflowInstanceId,
                activityId,
                activityName,
                activityType,
                assignedTo,
                inputData);

            execution.Start();

            await _executionRepository.AddAsync(execution, cancellationToken);
            await _executionRepository.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Created activity execution for {ActivityId} in workflow {WorkflowInstanceId}",
                activityId, workflowInstanceId);

            return execution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create activity execution for {ActivityId} in workflow {WorkflowInstanceId}",
                activityId, workflowInstanceId);
            throw;
        }
    }

    public async Task UpdateActivityExecutionAsync(WorkflowActivityExecution activityExecution,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _executionRepository.UpdateAsync(activityExecution, cancellationToken);
            await _executionRepository.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Updated activity execution {ExecutionId}", activityExecution.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update activity execution {ExecutionId}", activityExecution.Id);
            throw;
        }
    }

    public async Task<WorkflowActivityExecution?> GetCurrentActivityExecutionAsync(Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _executionRepository.GetCurrentActivityForWorkflowAsync(workflowInstanceId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current activity execution for workflow {WorkflowInstanceId}",
                workflowInstanceId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowInstance>> GetUserTasksAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _instanceRepository.GetByAssignee(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user tasks for {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesForUserAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _executionRepository.GetCurrentActivitiesForUserAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current activities for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _executionRepository.GetCurrentActivitiesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current activities");
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        // ENHANCED: Use EF Core's execution strategy for automatic retry on transient failures
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        
        await strategy.ExecuteAsync(async () =>
        {
            // Centralized transaction boundary for workflow persistence operations.
            // Uses the scoped WorkflowDbContext so all repositories participate in the same transaction.
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await operation();

                // Do not force SaveChanges here; allow the operation to control SaveChanges timing.
                await transaction.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Ensure rollback on cancellation and rethrow to propagate cancellation upstream.
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    /* ignore rollback failures */
                }

                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch
                {
                    /* ignore rollback failures */
                }

                _logger.LogError(ex, "Workflow persistence transaction failed and was rolled back");
                throw;
            }
        });
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // ENHANCED: Use EF Core's execution strategy for automatic retry on transient failures
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (OperationCanceledException)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // ignored - transaction may already be disposed
                }

                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch
                {
                    // ignored - transaction may already be disposed
                }

                _logger.LogError(ex, "Workflow persistence transaction (with result) failed and was rolled back");
                throw;
            }
        });
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        // ENHANCED: Use EF Core's execution strategy for automatic retry on transient failures
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

            try
            {
                await operation();
                await transaction.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // ignored - transaction may already be disposed
                }

                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch
                {
                    // ignored - transaction may already be disposed
                }

                _logger.LogError(ex, "Workflow persistence transaction (iso={Isolation}) failed and was rolled back",
                    isolationLevel);
                throw;
            }
        });
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        // ENHANCED: Use EF Core's execution strategy for automatic retry on transient failures
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (OperationCanceledException)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // ignored - transaction may already be disposed
                }

                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch
                {
                    // ignored - transaction may already be disposed
                }

                _logger.LogError(ex,
                    "Workflow persistence transaction (with result, iso={Isolation}) failed and was rolled back",
                    isolationLevel);
                throw;
            }
        });
    }

    public async Task<T> ExecuteInTransactionWithStrategyAsync<T>(Func<Task<T>> operation,
        IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(ExecuteOperation, cancellationToken);

        async Task<T> ExecuteOperation(CancellationToken ct)
        {
            return await ExecuteInTransactionAsync(operation, isolationLevel, ct);
        }
    }

    public async Task<int> AcquireApplicationLockAsync(
        string lockResource,
        string lockMode = "Exclusive",
        int timeoutMs = 30000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _sqlConnectionFactory.GetOpenConnection();

            var parameters = new DynamicParameters();
            parameters.Add("Resource", lockResource);
            parameters.Add("LockMode", lockMode);
            parameters.Add("LockOwner", "Transaction");
            parameters.Add("LockTimeout", timeoutMs);
            parameters.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

            await connection.ExecuteAsync(
                "sp_getapplock",
                parameters,
                commandType: CommandType.StoredProcedure,
                transaction: _dbContext.Database.CurrentTransaction?.GetDbTransaction());

            var result = parameters.Get<int>("ReturnValue");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire application lock: {LockResource}", lockResource);
            throw;
        }
    }

    /// <summary>
    /// Securely deserializes workflow schema JSON with validation
    /// </summary>
    private static WorkflowSchema? DeserializeWorkflowSchemaSecurely(string jsonDefinition, Guid workflowDefinitionId)
    {
        // Validate JSON size to prevent memory exhaustion
        if (jsonDefinition.Length > WorkflowEngineConstants.MaxWorkflowDefinitionJsonSize)
            throw new InvalidOperationException(
                $"Workflow definition JSON exceeds maximum size limit for: {workflowDefinitionId}");

        // Configure secure JSON options
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = WorkflowEngineConstants
                .MaxJsonDeserializationDepth, // Prevent stack overflow from deeply nested objects
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        try
        {
            var workflowSchema = JsonSerializer.Deserialize<WorkflowSchema>(jsonDefinition, jsonOptions);

            // Additional validation
            if (workflowSchema != null) ValidateWorkflowSchemaStructure(workflowSchema);

            return workflowSchema;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Invalid JSON format in workflow definition {workflowDefinitionId}: {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new InvalidOperationException(
                $"Unsupported JSON structure in workflow definition {workflowDefinitionId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates workflow schema structure for security and correctness
    /// </summary>
    private static void ValidateWorkflowSchemaStructure(WorkflowSchema workflowSchema)
    {
        if (string.IsNullOrWhiteSpace(workflowSchema.Name))
            throw new InvalidOperationException("Workflow schema must have a valid name");

        if (workflowSchema.Activities == null || !workflowSchema.Activities.Any())
            throw new InvalidOperationException("Workflow schema must have at least one activity");

        // Validate activity count to prevent resource exhaustion
        if (workflowSchema.Activities.Count > WorkflowEngineConstants.MaxActivitiesPerWorkflow)
            throw new InvalidOperationException(
                $"Workflow schema exceeds maximum activity limit of {WorkflowEngineConstants.MaxActivitiesPerWorkflow}");

        // Validate transitions count
        if (workflowSchema.Transitions?.Count > WorkflowEngineConstants.MaxTransitionsPerWorkflow)
            throw new InvalidOperationException(
                $"Workflow schema exceeds maximum transition limit of {WorkflowEngineConstants.MaxTransitionsPerWorkflow}");

        // Validate activity IDs are unique and safe
        var activityIds = new HashSet<string>();
        foreach (var activity in workflowSchema.Activities)
        {
            if (string.IsNullOrWhiteSpace(activity.Id))
                throw new InvalidOperationException("All activities must have valid IDs");

            if (!activityIds.Add(activity.Id))
                throw new InvalidOperationException($"Duplicate activity ID found: {activity.Id}");

            // Validate activity ID format (alphanumeric, underscore, hyphen only)
            if (!System.Text.RegularExpressions.Regex.IsMatch(activity.Id, @"^[a-zA-Z0-9_-]+$"))
                throw new InvalidOperationException($"Activity ID contains invalid characters: {activity.Id}");
        }
    }
}