using Workflow.DocumentFollowups.Application.Commands;
using Workflow.DocumentFollowups.Application.Queries;

namespace Workflow.DocumentFollowups.Endpoints;

// ─────────────────────────────────────────────────────────────────────────────
// Request body DTOs (kept here so endpoint module is self-contained)
// ─────────────────────────────────────────────────────────────────────────────

public record RaiseDocumentFollowupRequest(
    Guid RaisingWorkflowInstanceId,
    Guid RaisingTaskId,
    IReadOnlyList<RaiseDocumentFollowupLineItemDto> LineItems);

public record CancelDocumentFollowupRequest(string Reason);

public record DeclineDocumentFollowupLineItemRequest(string Reason);

// ─────────────────────────────────────────────────────────────────────────────

public class DocumentFollowupEndpoints : ICarterModule
{
    private const string Base = "/workflows/document-followups";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(Base, async (
                RaiseDocumentFollowupRequest body, ISender sender, CancellationToken ct) =>
            {
                var cmd = new RaiseDocumentFollowupCommand(
                    body.RaisingWorkflowInstanceId,
                    body.RaisingTaskId,
                    body.LineItems);
                var result = await sender.Send(cmd, ct);
                return Results.Ok(result);
            })
            .WithName("RaiseDocumentFollowup")
            .WithTags("DocumentFollowups")
            .RequireAuthorization();

        app.MapPost($"{Base}/{{id:guid}}/cancel", async (
                Guid id, CancelDocumentFollowupRequest body, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new CancelDocumentFollowupCommand(id, body.Reason), ct);
                return Results.NoContent();
            })
            .WithName("CancelDocumentFollowup")
            .WithTags("DocumentFollowups")
            .RequireAuthorization();

        app.MapPost($"{Base}/{{id:guid}}/line-items/{{lineItemId:guid}}/cancel", async (
                Guid id, Guid lineItemId, CancelDocumentFollowupRequest body, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new CancelDocumentFollowupLineItemCommand(id, lineItemId, body.Reason), ct);
                return Results.NoContent();
            })
            .WithName("CancelDocumentFollowupLineItem")
            .WithTags("DocumentFollowups")
            .RequireAuthorization();

        app.MapPost($"{Base}/{{id:guid}}/line-items/{{lineItemId:guid}}/decline", async (
                Guid id, Guid lineItemId, DeclineDocumentFollowupLineItemRequest body, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeclineLineItemCommand(id, lineItemId, body.Reason), ct);
                return Results.NoContent();
            })
            .WithName("DeclineDocumentFollowupLineItem")
            .WithTags("DocumentFollowups")
            .RequireAuthorization();

        app.MapPost($"{Base}/{{id:guid}}/submit", async (
                Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new SubmitDocumentFollowupCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("SubmitDocumentFollowup")
            .WithTags("DocumentFollowups")
            .RequireAuthorization();

        app.MapGet(Base, async (
                Guid? raisingTaskId,
                string? status,
                Guid? followupWorkflowInstanceId,
                ISender sender,
                CancellationToken ct) =>
            {
                var rows = await sender.Send(
                    new ListDocumentFollowupsQuery(raisingTaskId, status, followupWorkflowInstanceId), ct);
                return Results.Ok(rows);
            })
            .WithName("ListDocumentFollowups")
            .WithTags("DocumentFollowups")
            .RequireAuthorization();

        app.MapGet($"{Base}/{{id:guid}}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var dto = await sender.Send(new GetDocumentFollowupByIdQuery(id), ct);
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            })
            .WithName("GetDocumentFollowupById")
            .WithTags("DocumentFollowups")
            .RequireAuthorization();
    }
}
