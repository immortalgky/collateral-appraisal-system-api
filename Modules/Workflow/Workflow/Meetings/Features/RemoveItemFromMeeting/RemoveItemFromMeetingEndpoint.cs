using Workflow.Meetings.Domain;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.Features.RemoveItemFromMeeting;

public class RemoveItemFromMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/meetings/{id:guid}/items/{appraisalId:guid}", async (
                Guid id,
                Guid appraisalId,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new RemoveItemFromMeetingCommand(id, appraisalId), ct);
                return Results.NoContent();
            })
            .WithName("RemoveItemFromMeeting")
            .WithTags("Meetings")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record RemoveItemFromMeetingCommand(Guid MeetingId, Guid AppraisalId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class RemoveItemFromMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    WorkflowDbContext dbContext)
    : ICommandHandler<RemoveItemFromMeetingCommand>
{
    public async Task<Unit> Handle(RemoveItemFromMeetingCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdWithItemsAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.RemoveItem(command.AppraisalId);

        // Return the queue item back to Queued
        var queueItem = await dbContext.MeetingQueueItems
            .FirstOrDefaultAsync(q =>
                q.AppraisalId == command.AppraisalId &&
                q.MeetingId == command.MeetingId &&
                q.Status == MeetingQueueItemStatus.Assigned, ct);
        queueItem?.ReturnToQueue();

        return Unit.Value;
    }
}
