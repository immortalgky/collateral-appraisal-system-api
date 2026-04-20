using MediatR;
using Request.Contracts.RequestDocuments;
using Shared.Identity;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.Application.Commands;

public class SubmitDocumentFollowupCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    IRequestDocumentAttacher documentAttacher,
    ICurrentUserService currentUser,
    IPublisher publisher,
    ILogger<SubmitDocumentFollowupCommandHandler> logger
) : ICommandHandler<SubmitDocumentFollowupCommand, Unit>
{
    public async Task<Unit> Handle(SubmitDocumentFollowupCommand command, CancellationToken cancellationToken)
    {
        var followup = await dbContext.DocumentFollowups
                           .FirstOrDefaultAsync(f => f.Id == command.FollowupId, cancellationToken)
                       ?? throw new InvalidOperationException($"Document followup {command.FollowupId} not found");

        var actor = currentUser.Username ?? throw new InvalidOperationException("User not authenticated");

        // Authorization: only the assignee of the followup child workflow task can submit.
        // Fail closed: if not yet provisioned, reject.
        if (!followup.FollowupWorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Followup not fully provisioned");

        var fwInstance = await dbContext.WorkflowInstances
                             .AsNoTracking()
                             .FirstOrDefaultAsync(w => w.Id == followup.FollowupWorkflowInstanceId.Value,
                                 cancellationToken)
                         ?? throw new InvalidOperationException("Followup workflow instance not found");

        if (!string.Equals(fwInstance.StartedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                "Only the request maker assigned to this followup can submit it");

        // Validate attachments up front so we fail before any cross-module writes.
        var attachments = command.Attachments ?? new List<SubmitFollowupAttachmentDto>();
        ValidateAttachments(followup, attachments);

        // Perform attach + fulfill. Each Request/Title aggregate write commits on its own
        // DbContext (different transaction scope than the Workflow transaction). We validate
        // up front; if infra fails mid-batch, surface an error for ops to reconcile.
        foreach (var attachment in attachments)
        {
            if (!followup.RequestId.HasValue)
                throw new InvalidOperationException(
                    "Followup has no associated RequestId — cannot attach documents.");

            var input = new AttachedDocumentInput(
                attachment.DocumentId,
                attachment.DocumentType,
                attachment.FileName,
                UploadedBy: actor,
                UploadedByName: actor);

            if (attachment.AttachToRequest)
            {
                await documentAttacher.AttachToRequestAsync(
                    followup.RequestId.Value, input, cancellationToken);
            }
            else
            {
                if (!attachment.TitleId.HasValue)
                    throw new InvalidOperationException(
                        $"Attachment for line item {attachment.LineItemId} missing TitleId.");

                await documentAttacher.AttachToTitleAsync(
                    followup.RequestId.Value, attachment.TitleId.Value, input, cancellationToken);
            }

            followup.FulfillByUpload(attachment.LineItemId, attachment.DocumentId);
        }

        followup.Submit(actor);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var ev in followup.ClearDomainEvents())
            await publisher.Publish(ev, cancellationToken);

        // Single path back to the raiser — always signal "P" (proceed) regardless of
        // whether items were Uploaded or Declined.
        await workflowService.ResumeWorkflowAsync(
            followup.FollowupWorkflowInstanceId.Value,
            fwInstance.CurrentActivityId,
            actor,
            new Dictionary<string, object> { ["decisionTaken"] = "P" },
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Document followup {FollowupId} submitted by {Actor} with {AttachmentCount} attachment(s)",
            command.FollowupId, actor, attachments.Count);
        return Unit.Value;
    }

    private static void ValidateAttachments(
        DocumentFollowup followup,
        IReadOnlyList<SubmitFollowupAttachmentDto> attachments)
    {
        // Each attachment must target a Pending line item on this followup, and documentType
        // must match. Every Pending line item must be covered (either by an attachment or by
        // an already-Declined status — the aggregate's Submit will re-check).
        var pendingItems = followup.LineItems
            .Where(li => li.Status == DocumentFollowupLineItemStatus.Pending)
            .ToDictionary(li => li.Id);

        var seenLineItemIds = new HashSet<Guid>();
        foreach (var a in attachments)
        {
            if (!pendingItems.TryGetValue(a.LineItemId, out var item))
                throw new InvalidOperationException(
                    $"Attachment references unknown or already-resolved line item {a.LineItemId}.");

            if (!seenLineItemIds.Add(a.LineItemId))
                throw new InvalidOperationException(
                    $"Duplicate attachment for line item {a.LineItemId}.");

            if (!string.Equals(a.DocumentType, item.DocumentType, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Attachment document type '{a.DocumentType}' does not match line item " +
                    $"{a.LineItemId} expected type '{item.DocumentType}'.");

            if (a.DocumentId == Guid.Empty)
                throw new InvalidOperationException(
                    $"Attachment for line item {a.LineItemId} has empty DocumentId.");

            if (string.IsNullOrWhiteSpace(a.FileName))
                throw new InvalidOperationException(
                    $"Attachment for line item {a.LineItemId} has empty FileName.");

            if (!a.AttachToRequest && !a.TitleId.HasValue)
                throw new InvalidOperationException(
                    $"Attachment for line item {a.LineItemId} must specify TitleId when AttachToRequest is false.");
        }

        // Every Pending line item must have a matching attachment — the aggregate's Submit
        // rejects still-Pending items, so surface the friendlier message here.
        var missing = pendingItems.Keys.Except(seenLineItemIds).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Missing attachments for {missing.Count} pending line item(s). " +
                "Provide a file for every pending item (or decline it) before submitting.");
    }
}
