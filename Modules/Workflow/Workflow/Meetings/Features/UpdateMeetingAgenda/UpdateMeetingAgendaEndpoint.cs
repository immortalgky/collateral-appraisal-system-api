using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.UpdateMeetingAgenda;

public class UpdateMeetingAgendaEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapMethods("/meetings/{id:guid}/agenda", ["PATCH"], async (
                Guid id,
                UpdateMeetingAgendaRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new UpdateMeetingAgendaCommand(id, request), ct);
                return Results.NoContent();
            })
            .WithName("UpdateMeetingAgenda")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record UpdateMeetingAgendaRequest(
    string? FromText,
    string? ToText,
    string? AgendaCertifyMinutes,
    string? AgendaChairmanInformed,
    string? AgendaOthers);

public record UpdateMeetingAgendaCommand(Guid MeetingId, UpdateMeetingAgendaRequest Request)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class UpdateMeetingAgendaCommandHandler(
    IMeetingRepository meetingRepository,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateMeetingAgendaCommand>
{
    public async Task<Unit> Handle(UpdateMeetingAgendaCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.SetAgenda(
            command.Request.FromText,
            command.Request.ToText,
            command.Request.AgendaCertifyMinutes,
            command.Request.AgendaChairmanInformed,
            command.Request.AgendaOthers,
            dateTimeProvider.ApplicationNow);

        return Unit.Value;
    }
}
