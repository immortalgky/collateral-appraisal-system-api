using MediatR;
using Microsoft.Extensions.Logging;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Domain.RequestTitles;
using Shared.CQRS;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Messaging.Events;
using Workflow.Contracts.DocumentFollowups;

namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;

public class ResubmitRequestCommandHandler(
    IUpdateRequestService updateRequestService,
    IRequestSyncService syncService,
    ISender mediator,
    IIntegrationEventOutbox outbox,
    ILogger<ResubmitRequestCommandHandler> logger
) : ICommandHandler<ResubmitRequestCommand, ResubmitRequestResult>
{
    public async Task<ResubmitRequestResult> Handle(ResubmitRequestCommand command, CancellationToken cancellationToken)
    {
        // Don't try/catch and return Status:"Error" here. The TransactionalBehavior pipeline
        // only rolls back when this handler throws — swallowing means a partial commit + a
        // 200-OK response with an error envelope. Let exceptions propagate.
        var mode = ParseMode(command.Mode);
        return mode == ResubmitMode.Followup
            ? await HandleFollowupResubmitAsync(command, cancellationToken)
            : await HandleDataResubmitAsync(command, cancellationToken);
    }

    /// <summary>
    /// Parses the wire-level Mode string. Null or whitespace falls back to <see cref="ResubmitMode.DataFix"/>
    /// for back-compat with existing bank callers that don't know about the field.
    /// Unknown values throw — fail fast rather than silently picking a branch.
    /// </summary>
    private static ResubmitMode ParseMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ResubmitMode.DataFix;

        if (Enum.TryParse<ResubmitMode>(raw, ignoreCase: true, out var parsed))
            return parsed;

        throw new BadRequestException(
            $"Unknown resubmit Mode '{raw}'. Expected 'DataFix' or 'Followup'.");
    }

    // ─── Data-fix branch (existing behavior + workflow resume) ────────────────────────────────

    private async Task<ResubmitRequestResult> HandleDataResubmitAsync(
        ResubmitRequestCommand command, CancellationToken ct)
    {
        // Command fields are nullable so the followup branch can omit them. On the data-fix
        // branch they are required — reject up front rather than coalescing to empty strings
        // (which UpdateRequestService would happily commit) or hitting NREs in
        // ResubmitRequestAsync's downstream dereferences. Use BadRequestException so the
        // CustomExceptionHandler maps these to HTTP 400; ArgumentException-based helpers
        // would fall through to 500.
        if (string.IsNullOrWhiteSpace(command.Purpose))
            throw new BadRequestException("Purpose is required.");
        if (string.IsNullOrWhiteSpace(command.Channel))
            throw new BadRequestException("Channel is required.");
        if (string.IsNullOrWhiteSpace(command.Priority))
            throw new BadRequestException("Priority is required.");
        if (command.IsPma is null)
            throw new BadRequestException("IsPma is required.");
        if (command.Requestor is null)
            throw new BadRequestException("Requestor is required.");
        if (command.Creator is null)
            throw new BadRequestException("Creator is required.");
        if (command.Detail is null)
            throw new BadRequestException("Detail is required.");
        if (command.Customers is null)
            throw new BadRequestException("Customers is required.");
        if (command.Properties is null)
            throw new BadRequestException("Properties is required.");

        var resubmitData = new ResubmitRequestData(
            command.RequestId,
            command.Purpose,
            command.Channel,
            command.Requestor,
            command.Creator,
            command.Priority,
            command.IsPma.Value,
            command.Detail,
            command.Customers,
            command.Properties,
            command.Titles,
            command.Documents,
            Comments: null);

        var request = await updateRequestService.ResubmitRequestAsync(resubmitData, ct);

        if (command.Documents is not null)
            await syncService.SyncDocumentsAsync(request, command.Documents, ct, forcedSource: "REQUEST");

        IReadOnlyList<RequestTitle> titles = [];
        if (command.Titles is not null)
            titles = await syncService.SyncTitlesAsync(command.RequestId, command.Titles, ct, forcedSource: "REQUEST");

        request.Validate();
        foreach (var title in titles)
            title.Validate();

        // Publish to the persistent outbox so the Workflow-side resume happens AFTER the Request
        // transaction commits. A direct mediator.Send into Workflow would write through a different
        // DbContext/connection and commit independently — breaking atomicity if the outer Request
        // transaction later rolls back. See feedback_cross_module_outbox.
        outbox.Publish(
            new RequestResubmittedIntegrationEvent
            {
                RequestId = command.RequestId,
                FollowupId = null,
                FollowupItems = []
            },
            correlationId: command.RequestId.ToString());

        return new ResubmitRequestResult(
            status: "Success",
            message: "Request initiated successfully.");
    }

    // ─── Document-followup branch ─────────────────────────────────────────────────────────────

    private async Task<ResubmitRequestResult> HandleFollowupResubmitAsync(
        ResubmitRequestCommand command, CancellationToken ct)
    {
        // Reject mixed payloads: Mode=Followup + request-mutation fields are mutually exclusive.
        if (command.Purpose is not null || command.Channel is not null ||
            command.Detail is not null || command.Customers is not null || command.Properties is not null)
        {
            throw new BadRequestException(
                "Mode=Followup set together with request-mutation fields (Purpose/Channel/Detail/Customers/Properties) " +
                "— these are mutually exclusive.");
        }

        // Discover the open DocumentFollowup for this request. The bank doesn't echo back our
        // internal FollowupId — the server resolves it from open state. The data model permits
        // multiple open followups per request across distinct raising tasks, but today's workflow
        // never forks; 0 or 1 is the expected case.
        var openFollowups = await mediator.Send(
            new GetOpenDocumentFollowupForRequestQuery(command.RequestId), ct);

        if (openFollowups.Count == 0)
            throw new BadRequestException(
                $"No open document followup found for request {command.RequestId}.");

        if (openFollowups.Count > 1)
            // Server-state conflict — the bank can't fix this by altering the request body.
            throw new ConflictException(
                $"Multiple open document followups for request {command.RequestId} — cannot disambiguate.");

        var followup = openFollowups[0];

        if (!followup.FollowupWorkflowInstanceId.HasValue)
            // The followup exists but the child workflow hasn't been attached yet — transient
            // server-state condition, retryable by the caller.
            throw new ConflictException(
                $"Followup {followup.Id} is not fully provisioned — workflow instance not yet attached.");

        // Sync documents, trusting the payload's Source per row (forcedSource: null).
        // The followup branch does NOT mutate request data, only the document/title collections.
        if (command.Documents is not null)
        {
            var requestAggregate = await updateRequestService.GetByIdWithDocumentsAsync(command.RequestId, ct);
            await syncService.SyncDocumentsAsync(requestAggregate, command.Documents, ct, forcedSource: null);
        }

        IReadOnlyList<RequestTitle> titles = [];
        if (command.Titles is not null)
            titles = await syncService.SyncTitlesAsync(command.RequestId, command.Titles, ct, forcedSource: null);

        foreach (var title in titles)
            title.Validate();

        // Build the list of FOLLOWUP-sourced items to fulfill line items.
        // REQUEST-sourced docs in the payload are kept/synced above but don't count toward fulfillment.
        var followupItems = (command.Documents ?? [])
            .Where(d => string.Equals(d.Source, "FOLLOWUP", StringComparison.OrdinalIgnoreCase)
                        && d.DocumentId.HasValue)
            .Select(d => new FulfillFollowupItemDto(d.DocumentType, d.DocumentId!.Value))
            .ToList();

        // Also include FOLLOWUP-sourced docs from title-level documents.
        if (command.Titles is not null)
        {
            foreach (var titleDto in command.Titles)
            {
                foreach (var doc in titleDto.Documents
                    .Where(d => string.Equals(d.Source, "FOLLOWUP", StringComparison.OrdinalIgnoreCase)
                                && d.DocumentId.HasValue))
                {
                    followupItems.Add(new FulfillFollowupItemDto(doc.DocumentType!, doc.DocumentId!.Value));
                }
            }
        }

        // Publish to the persistent outbox so the Workflow-side fulfill+resume happens AFTER the
        // Request transaction commits. See data-fix branch above for the atomicity rationale.
        outbox.Publish(
            new RequestResubmittedIntegrationEvent
            {
                RequestId = command.RequestId,
                FollowupId = followup.Id,
                FollowupItems = followupItems
                    .Select(i => new ResubmittedFollowupItem(i.DocumentType, i.DocumentId))
                    .ToList()
            },
            correlationId: command.RequestId.ToString());

        logger.LogInformation(
            "Followup resubmit queued for RequestId {RequestId}, FollowupId {FollowupId}, " +
            "{ItemCount} FOLLOWUP items",
            command.RequestId, followup.Id, followupItems.Count);

        return new ResubmitRequestResult(
            status: "Success",
            message: "Document followup fulfilled successfully.");
    }
}
