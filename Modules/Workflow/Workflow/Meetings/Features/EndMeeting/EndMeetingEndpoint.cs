using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.EndMeeting;

public class EndMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/end", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new EndMeetingCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("EndMeeting")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record EndMeetingCommand(Guid MeetingId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class EndMeetingCommandHandler(IMeetingRepository meetingRepository, IDateTimeProvider dateTimeProvider)
    : ICommandHandler<EndMeetingCommand>
{
    public async Task<Unit> Handle(EndMeetingCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdWithItemsAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.End(dateTimeProvider.ApplicationNow);
        return Unit.Value;
    }
}
