using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.ScheduleMeeting;

public class ScheduleMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/schedule", async (
                Guid id,
                ScheduleMeetingRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new ScheduleMeetingCommand(id, request), ct);
                return Results.NoContent();
            })
            .WithName("ScheduleMeeting")
            .WithTags("Meetings")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record ScheduleMeetingRequest(DateTime ScheduledAt, string? Location);

public record ScheduleMeetingCommand(Guid MeetingId, ScheduleMeetingRequest Request)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class ScheduleMeetingCommandHandler(IMeetingRepository meetingRepository)
    : ICommandHandler<ScheduleMeetingCommand>
{
    public async Task<Unit> Handle(ScheduleMeetingCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdWithItemsAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.Schedule(command.Request.ScheduledAt, command.Request.Location);
        return Unit.Value;
    }
}
