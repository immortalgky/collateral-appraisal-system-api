using Microsoft.Extensions.Logging;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Services;

public class WorkflowSignalDispatcher(
    IWorkflowBookmarkRepository bookmarkRepository,
    IWorkflowService workflowService,
    ILogger<WorkflowSignalDispatcher> logger) : IWorkflowSignalDispatcher
{
    public async Task DispatchAsync(
        string signalName,
        string correlationValue,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default)
    {
        var bookmarks = await bookmarkRepository.GetBookmarksByCorrelationAsync(
            correlationValue, onlyUnconsumed: true, cancellationToken);

        var matchingBookmarks = bookmarks
            .Where(b => b.Key == signalName && b.Type == BookmarkType.ExternalMessage)
            .ToList();

        if (matchingBookmarks.Count == 0)
        {
            logger.LogWarning(
                "No active signal bookmarks found for signal '{SignalName}' with correlation '{CorrelationValue}'",
                signalName, correlationValue);
            return;
        }

        foreach (var bookmark in matchingBookmarks)
        {
            // Guard: skip if already consumed by a concurrent dispatch.
            // The EF-tracked entity may have been consumed between the query
            // and this iteration if another consumer processed the same event.
            if (bookmark.IsConsumed)
            {
                logger.LogInformation(
                    "Signal bookmark {BookmarkId} already consumed, skipping",
                    bookmark.Id);
                continue;
            }

            // Mark consumed on the EF-tracked entity. Do NOT call SaveChangesAsync
            // here — the caller's outer transaction flushes this atomically with
            // the variable update and workflow resume.
            bookmark.Consume("SYSTEM");

            var resumeInput = new Dictionary<string, object>(payload)
            {
                ["completedBy"] = "SYSTEM",
                ["signalName"] = signalName
            };

            await workflowService.ResumeWorkflowAsync(
                bookmark.WorkflowInstanceId,
                bookmark.ActivityId,
                "SYSTEM",
                resumeInput,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Signal '{SignalName}' dispatched to workflow {WorkflowInstanceId} at activity {ActivityId}",
                signalName, bookmark.WorkflowInstanceId, bookmark.ActivityId);
        }
    }
}
