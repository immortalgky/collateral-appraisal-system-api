using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.CancelMeeting;

public class CancelMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/cancel", async (
                Guid id,
                CancelMeetingRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new CancelMeetingCommand(id, request.Reason), ct);
                return Results.NoContent();
            })
            .WithName("CancelMeeting")
            .WithTags("Meetings")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record CancelMeetingRequest(string? Reason);

public record CancelMeetingCommand(Guid MeetingId, string? Reason)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class CancelMeetingCommandHandler(IMeetingRepository meetingRepository)
    : ICommandHandler<CancelMeetingCommand>
{
    public async Task<Unit> Handle(CancelMeetingCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdWithItemsAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.Cancel(command.Reason);
        return Unit.Value;
    }
}
