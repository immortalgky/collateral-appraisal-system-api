using Workflow.Meetings.Domain;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.Features.CutOffMeeting;

public class CutOffMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/cut-off", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new CutOffMeetingCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("CutOffMeeting")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record CutOffMeetingCommand(Guid MeetingId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class CutOffMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    WorkflowDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CutOffMeetingCommand>
{
    public async Task<Unit> Handle(CutOffMeetingCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdWithItemsAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        // Load queued items not yet assigned to any meeting
        var queueItems = await dbContext.MeetingQueueItems
            .Where(q => q.Status == MeetingQueueItemStatus.Queued && q.MeetingId == null)
            .ToListAsync(ct);

        // Load acknowledgement items pending for the meeting
        var ackItems = await dbContext.AppraisalAcknowledgementQueueItems
            .Where(a => a.Status == AcknowledgementStatus.PendingAcknowledgement)
            .ToListAsync(ct);

        meeting.CutOff(queueItems, ackItems, dateTimeProvider.ApplicationNow);

        return Unit.Value;
    }
}
