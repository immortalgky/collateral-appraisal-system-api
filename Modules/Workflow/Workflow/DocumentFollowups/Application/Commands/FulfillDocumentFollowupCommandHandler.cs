using Workflow.Contracts.DocumentFollowups;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.Application.Commands;

/// <summary>
/// System-actor handler for fulfilling a document followup from the Integration module's
/// external resubmit endpoint.
///
/// Differences from SubmitDocumentFollowupCommandHandler:
/// - Does NOT assert currentUser.Username == fwInstance.StartedBy (system actor).
/// - Matches line items by DocumentType, not by LineItemId.
/// - Does NOT call IRequestDocumentAttacher (documents are already synced by RequestSyncService).
/// - Takes an explicit Actor parameter rather than reading ICurrentUserService.
/// </summary>
public class FulfillDocumentFollowupCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    ILogger<FulfillDocumentFollowupCommandHandler> logger
) : ICommandHandler<FulfillDocumentFollowupCommand, Unit>
{
    public async Task<Unit> Handle(FulfillDocumentFollowupCommand command, CancellationToken cancellationToken)
    {
        var followup = await dbContext.DocumentFollowups
                           .FirstOrDefaultAsync(f => f.Id == command.FollowupId, cancellationToken)
                       ?? throw new InvalidOperationException($"Document followup {command.FollowupId} not found");

        if (!followup.FollowupWorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Followup is not fully provisioned — workflow instance not yet attached");

        var fwInstance = await dbContext.WorkflowInstances
                             .AsNoTracking()
                             .FirstOrDefaultAsync(w => w.Id == followup.FollowupWorkflowInstanceId.Value, cancellationToken)
                         ?? throw new InvalidOperationException("Followup workflow instance not found");

        // Validate that the FOLLOWUP-sourced docs cover all pending line items.
        // Only items in command.FollowupItems count toward coverage (they are FOLLOWUP-sourced).
        ValidateFollowupItems(followup, command.FollowupItems);

        // Fulfill each line item by matching DocumentType.
        foreach (var item in command.FollowupItems)
        {
            var matched = followup.FulfillFirstMatchingByType(item.DocumentType, item.DocumentId);
            if (matched is null)
            {
                // ValidateFollowupItems already caught duplicates and unknowns; this is a safeguard.
                throw new InvalidOperationException(
                    $"No pending line item matched DocumentType '{item.DocumentType}' during fulfill.");
            }
        }

        followup.Submit(command.Actor);
        await dbContext.SaveChangesAsync(cancellationToken);
        // DispatchDomainEventInterceptor drained the aggregate's domain events during the save
        // above — no separate post-save publish loop is needed (and adding one would be a no-op).

        // Resume the followup child workflow. Read CurrentActivityId from the loaded instance
        // rather than hardcoding the activity string, per the design plan.
        await workflowService.ResumeWorkflowAsync(
            followup.FollowupWorkflowInstanceId.Value,
            fwInstance.CurrentActivityId,
            command.Actor,
            new Dictionary<string, object> { ["decisionTaken"] = "P" },
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Document followup {FollowupId} fulfilled by system actor {Actor} with {ItemCount} item(s)",
            command.FollowupId, command.Actor, command.FollowupItems.Count);

        return Unit.Value;
    }

    /// <summary>
    /// Validates that the incoming FOLLOWUP-sourced items exactly cover all Pending line items
    /// on the followup (one item per pending line item, matched by DocumentType).
    /// Mirrors the logic in SubmitDocumentFollowupCommandHandler.ValidateAttachments but
    /// operates on DocumentType instead of LineItemId.
    /// </summary>
    private static void ValidateFollowupItems(
        DocumentFollowup followup,
        IReadOnlyList<FulfillFollowupItemDto> items)
    {
        var pendingByType = followup.LineItems
            .Where(li => li.Status == DocumentFollowupLineItemStatus.Pending)
            .ToDictionary(li => li.DocumentType, StringComparer.OrdinalIgnoreCase);

        var seenTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!pendingByType.ContainsKey(item.DocumentType))
                throw new InvalidOperationException(
                    $"No pending line item with DocumentType '{item.DocumentType}' exists on followup {followup.Id}.");

            if (!seenTypes.Add(item.DocumentType))
                throw new InvalidOperationException(
                    $"Duplicate DocumentType '{item.DocumentType}' in fulfillment items.");

            if (item.DocumentId == Guid.Empty)
                throw new InvalidOperationException(
                    $"FulfillFollowupItemDto for DocumentType '{item.DocumentType}' has empty DocumentId.");
        }

        // Every pending line item must be covered.
        var missingTypes = pendingByType.Keys.Except(seenTypes, StringComparer.OrdinalIgnoreCase).ToList();
        if (missingTypes.Count > 0)
            throw new InvalidOperationException(
                $"Missing fulfillment items for {missingTypes.Count} pending line item(s): " +
                string.Join(", ", missingTypes));
    }
}
