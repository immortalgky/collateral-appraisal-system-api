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
            .RequireAuthorization()
            .Produces<CreateMeetingResponse>(StatusCodes.Status201Created);
    }
}

public record CreateMeetingRequest(string Title, string? Notes);

public record CreateMeetingCommand(CreateMeetingRequest Request)
    : ICommand<CreateMeetingResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CreateMeetingResponse(Guid Id, string Title, string Status);

public class CreateMeetingCommandHandler(IMeetingRepository meetingRepository)
    : ICommandHandler<CreateMeetingCommand, CreateMeetingResponse>
{
    public async Task<CreateMeetingResponse> Handle(CreateMeetingCommand command, CancellationToken ct)
    {
        var meeting = Meeting.Create(command.Request.Title, command.Request.Notes);
        await meetingRepository.AddAsync(meeting, ct);
        return new CreateMeetingResponse(meeting.Id, meeting.Title, meeting.Status.ToString());
    }
}
