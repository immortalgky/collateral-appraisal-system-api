using MediatR;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Commands;

/// <summary>
/// Handles activity completion with workflow progression
/// </summary>
public sealed class CompleteActivityCommandHandler : IRequestHandler<CompleteActivityCommand, CompleteActivityResult>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IWorkflowBookmarkService _bookmarkService;
    private readonly IWorkflowResilienceService _resilienceService;
    private readonly ILogger<CompleteActivityCommandHandler> _logger;

    public CompleteActivityCommandHandler(
        IWorkflowEngine workflowEngine,
        IWorkflowInstanceRepository workflowInstanceRepository,
        IWorkflowBookmarkService bookmarkService,
        IWorkflowResilienceService resilienceService,
        ILogger<CompleteActivityCommandHandler> logger)
    {
        _workflowEngine = workflowEngine;
        _workflowInstanceRepository = workflowInstanceRepository;
        _bookmarkService = bookmarkService;
        _resilienceService = resilienceService;
        _logger = logger;
    }

    public async Task<CompleteActivityResult> Handle(CompleteActivityCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("COMMAND: Completing activity {ActivityId} in workflow {WorkflowInstanceId} by {CompletedBy}",
                request.ActivityId, request.WorkflowInstanceId, request.CompletedBy);

            // Execute with resilience protection
            return await _resilienceService.ExecuteDatabaseOperationAsync(async ct =>
            {
                // 1. Load workflow instance
                var workflowInstance = await _workflowInstanceRepository.GetByIdAsync(request.WorkflowInstanceId, ct);
                if (workflowInstance == null)
                {
                    _logger.LogWarning("COMMAND: Workflow instance {WorkflowInstanceId} not found",
                        request.WorkflowInstanceId);
                    return new CompleteActivityResult(false, null, null, false, "Workflow instance not found");
                }

                // 2. Validate workflow is in a resumable state
                if (workflowInstance.Status != WorkflowStatus.Running && workflowInstance.Status != WorkflowStatus.Suspended)
                {
                    _logger.LogWarning("COMMAND: Cannot complete activity for workflow {WorkflowInstanceId} in status {Status}",
                        request.WorkflowInstanceId, workflowInstance.Status);
                    return new CompleteActivityResult(false, workflowInstance, null, false,
                        $"Workflow is not in a resumable state: {workflowInstance.Status}");
                }

                // 3. Validate the activity is the current activity
                if (workflowInstance.CurrentActivityId != request.ActivityId)
                {
                    _logger.LogWarning("COMMAND: Activity {ActivityId} is not the current activity {CurrentActivityId} for workflow {WorkflowInstanceId}",
                        request.ActivityId, workflowInstance.CurrentActivityId, request.WorkflowInstanceId);
                    return new CompleteActivityResult(false, workflowInstance, null, false,
                        $"Activity {request.ActivityId} is not the current activity");
                }

                // 4. If bookmark key is provided, consume it first
                if (!string.IsNullOrEmpty(request.BookmarkKey))
                {
                    var bookmarkResult = await _bookmarkService.ConsumeBookmarkAsync(
                        request.WorkflowInstanceId,
                        request.ActivityId,
                        request.BookmarkKey,
                        request.CompletedBy,
                        request.OutputData,
                        ct);

                    if (!bookmarkResult.Success)
                    {
                        _logger.LogWarning("COMMAND: Failed to consume bookmark {BookmarkKey} for activity {ActivityId}: {Error}",
                            request.BookmarkKey, request.ActivityId, bookmarkResult.ErrorMessage);
                        
                        // Continue anyway if bookmark consumption fails - might be a duplicate request
                        if (bookmarkResult.FailureReason != BookmarkConsumeFailureReason.BookmarkAlreadyConsumed)
                        {
                            return new CompleteActivityResult(false, workflowInstance, null, false,
                                $"Bookmark consumption failed: {bookmarkResult.ErrorMessage}");
                        }
                    }
                }

                // 5. Use WorkflowEngine to resume workflow with activity completion
                var resumeResult = await _workflowEngine.ResumeWorkflowAsync(
                    request.WorkflowInstanceId,
                    request.ActivityId,
                    request.CompletedBy,
                    request.OutputData,
                    null, // No runtime overrides for simple completion
                    ct);

                // 6. Map engine result to command result
                var success = resumeResult.Status == WorkflowExecutionStatus.Completed ||
                             resumeResult.Status == WorkflowExecutionStatus.StepCompleted ||
                             resumeResult.Status == WorkflowExecutionStatus.Pending;

                if (!success)
                {
                    _logger.LogError("COMMAND: Failed to complete activity {ActivityId} for workflow {WorkflowInstanceId}: {ErrorMessage}",
                        request.ActivityId, request.WorkflowInstanceId, resumeResult.ErrorMessage);
                    return new CompleteActivityResult(false, resumeResult.WorkflowInstance, null, false,
                        resumeResult.ErrorMessage ?? "Activity completion failed");
                }

                var workflowCompleted = resumeResult.Status == WorkflowExecutionStatus.Completed;
                var nextActivityId = resumeResult.Status == WorkflowExecutionStatus.StepCompleted ? 
                    resumeResult.NextActivityId : null;

                _logger.LogInformation("COMMAND: Successfully completed activity {ActivityId} for workflow {WorkflowInstanceId}. Status: {Status}, Next: {NextActivityId}",
                    request.ActivityId, request.WorkflowInstanceId, resumeResult.Status, nextActivityId);

                return new CompleteActivityResult(
                    true, 
                    resumeResult.WorkflowInstance, 
                    nextActivityId, 
                    workflowCompleted);

            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "COMMAND: Critical error completing activity {ActivityId} in workflow {WorkflowInstanceId}",
                request.ActivityId, request.WorkflowInstanceId);

            return new CompleteActivityResult(false, null, null, false,
                $"Critical error during activity completion: {ex.Message}");
        }
    }
}