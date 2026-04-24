using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.UpdateMeeting;

public class UpdateMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/meetings/{id:guid}", async (
                Guid id,
                UpdateMeetingRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new UpdateMeetingCommand(id, request), ct);
                return Results.NoContent();
            })
            .WithName("UpdateMeeting")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record UpdateMeetingRequest(
    string Title,
    string? Location,
    string? Notes,
    DateTime? StartAt,
    DateTime? EndAt);

public record UpdateMeetingCommand(Guid MeetingId, UpdateMeetingRequest Request)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class UpdateMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateMeetingCommand>
{
    public async Task<Unit> Handle(UpdateMeetingCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        var now = dateTimeProvider.ApplicationNow;
        meeting.UpdateDetails(command.Request.Title, command.Request.Location, command.Request.Notes, now);

        if (command.Request.StartAt.HasValue && command.Request.EndAt.HasValue)
            meeting.SetSchedule(command.Request.StartAt.Value, command.Request.EndAt.Value, command.Request.Location, now);

        return Unit.Value;
    }
}
