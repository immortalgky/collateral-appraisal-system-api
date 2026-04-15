using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.CreateMeeting;

public class CreateMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings", async (
                CreateMeetingRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateMeetingCommand(request), ct);
                return Results.Created($"/meetings/{result.Id}", result);
            })
            .WithName("CreateMeeting")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<CreateMeetingResponse>(StatusCodes.Status201Created);
    }
}

public record CreateMeetingRequest(
    string Title,
    string? Notes,
    Guid? CommitteeId,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location);

public record CreateMeetingCommand(CreateMeetingRequest Request)
    : ICommand<CreateMeetingResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CreateMeetingResponse(Guid Id, string Title, string Status);

public class CreateMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    ICommitteeRepository committeeRepository)
    : ICommandHandler<CreateMeetingCommand, CreateMeetingResponse>
{
    public async Task<CreateMeetingResponse> Handle(CreateMeetingCommand command, CancellationToken ct)
    {
        var meeting = Meeting.Create(command.Request.Title, command.Request.Notes);

        if (command.Request.CommitteeId.HasValue)
        {
            var committee = await committeeRepository.GetByIdWithMembersAsync(
                command.Request.CommitteeId.Value, ct)
                ?? throw new NotFoundException(
                    $"Committee {command.Request.CommitteeId.Value} not found");

            meeting.SnapshotCommittee(committee);
        }

        if (command.Request.StartAt.HasValue && command.Request.EndAt.HasValue)
            meeting.SetSchedule(command.Request.StartAt.Value, command.Request.EndAt.Value, command.Request.Location);

        await meetingRepository.AddAsync(meeting, ct);
        return new CreateMeetingResponse(meeting.Id, meeting.Title, meeting.Status.ToString());
    }
}
