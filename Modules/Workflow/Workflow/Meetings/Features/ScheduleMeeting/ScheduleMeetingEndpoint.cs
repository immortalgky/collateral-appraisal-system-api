using Workflow.Meetings.Domain;
using Workflow.Meetings.Features.SendInvitation;

namespace Workflow.Meetings.Features.ScheduleMeeting;

/// <summary>
/// DEPRECATED — kept as a compatibility alias for one release.
/// Clients should migrate to POST /meetings/{id}/send-invitation.
/// </summary>
[Obsolete("Use POST /meetings/{id}/send-invitation instead. This endpoint will be removed in a future release.")]
public class ScheduleMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/schedule", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                // Delegate to SendInvitation — sets StartAt/EndAt separately via UpdateMeeting
                var result = await sender.Send(new SendInvitationCommand(id), ct);
                return Results.Ok(result);
            })
            .WithName("ScheduleMeeting")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<SendInvitationResponse>()
            .WithDescription("DEPRECATED: use POST /meetings/{id}/send-invitation instead");
    }
}
