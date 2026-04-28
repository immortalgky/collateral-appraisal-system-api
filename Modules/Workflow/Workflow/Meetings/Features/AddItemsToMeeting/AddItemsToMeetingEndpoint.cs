using Workflow.Meetings.Domain;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.Features.AddItemsToMeeting;

public class AddItemsToMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/items", async (
                Guid id,
                AddItemsToMeetingRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new AddItemsToMeetingCommand(id, request), ct);
                return Results.NoContent();
            })
            .WithName("AddItemsToMeeting")
            .WithTags("Meetings")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record AddItemsToMeetingRequest(List<Guid> QueueItemIds);

public record AddItemsToMeetingCommand(Guid MeetingId, AddItemsToMeetingRequest Request)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class AddItemsToMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    WorkflowDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AddItemsToMeetingCommand>
{
    public async Task<Unit> Handle(AddItemsToMeetingCommand command, CancellationToken ct)
    {
        if (command.Request.QueueItemIds is null || command.Request.QueueItemIds.Count == 0)
            throw new ArgumentException("At least one queue item id is required");

        var meeting = await meetingRepository.GetByIdWithItemsAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        var queueItems = await dbContext.MeetingQueueItems
            .Where(q => command.Request.QueueItemIds.Contains(q.Id))
            .ToListAsync(ct);

        if (queueItems.Count != command.Request.QueueItemIds.Count)
        {
            var missing = command.Request.QueueItemIds.Except(queueItems.Select(q => q.Id));
            throw new NotFoundException($"Queue items not found: {string.Join(", ", missing)}");
        }

        var now = dateTimeProvider.ApplicationNow;

        foreach (var queueItem in queueItems)
        {
            if (queueItem.Status != MeetingQueueItemStatus.Queued)
                throw new InvalidOperationException(
                    $"Queue item {queueItem.Id} is not in Queued status (current: {queueItem.Status})");

            meeting.AddItem(
                queueItem.AppraisalId,
                queueItem.AppraisalNo,
                queueItem.FacilityLimit,
                queueItem.WorkflowInstanceId,
                queueItem.ActivityId,
                now);

            queueItem.AssignTo(meeting.Id);
        }

        return Unit.Value;
    }
}
