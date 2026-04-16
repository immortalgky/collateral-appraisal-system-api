using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.UpdateMeetingMembers;

/// <summary>Three endpoints for managing per-meeting member roster.</summary>
public class UpdateMeetingMembersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/members", async (
                Guid id,
                AddMeetingMemberRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new AddMeetingMemberCommand(id, request), ct);
                return Results.NoContent();
            })
            .WithName("AddMeetingMember")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);

        app.MapDelete("/meetings/{id:guid}/members/{memberId:guid}", async (
                Guid id,
                Guid memberId,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new RemoveMeetingMemberCommand(id, memberId), ct);
                return Results.NoContent();
            })
            .WithName("RemoveMeetingMember")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);

        app.MapMethods("/meetings/{id:guid}/members/{memberId:guid}", ["PATCH"], async (
                Guid id,
                Guid memberId,
                ChangeMemberPositionRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new ChangeMemberPositionCommand(id, memberId, request.Position), ct);
                return Results.NoContent();
            })
            .WithName("ChangeMeetingMemberPosition")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);
    }
}

// ----- Add member -----

public record AddMeetingMemberRequest(string UserId, string MemberName, CommitteeMemberPosition Position);

public record AddMeetingMemberCommand(Guid MeetingId, AddMeetingMemberRequest Request)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class AddMeetingMemberCommandHandler(IMeetingRepository meetingRepository)
    : ICommandHandler<AddMeetingMemberCommand>
{
    public async Task<Unit> Handle(AddMeetingMemberCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdForDecisionAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        var member = MeetingMember.CreateManual(
            command.MeetingId,
            command.Request.UserId,
            command.Request.MemberName,
            command.Request.Position);

        meeting.AddMember(member);
        return Unit.Value;
    }
}

// ----- Remove member -----

public record RemoveMeetingMemberCommand(Guid MeetingId, Guid MemberId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class RemoveMeetingMemberCommandHandler(IMeetingRepository meetingRepository)
    : ICommandHandler<RemoveMeetingMemberCommand>
{
    public async Task<Unit> Handle(RemoveMeetingMemberCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdForDecisionAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.RemoveMember(command.MemberId);
        return Unit.Value;
    }
}

// ----- Change position -----

public record ChangeMemberPositionRequest(CommitteeMemberPosition Position);

public record ChangeMemberPositionCommand(
    Guid MeetingId,
    Guid MemberId,
    CommitteeMemberPosition Position)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class ChangeMemberPositionCommandHandler(IMeetingRepository meetingRepository)
    : ICommandHandler<ChangeMemberPositionCommand>
{
    public async Task<Unit> Handle(ChangeMemberPositionCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdForDecisionAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.ChangeMemberPosition(command.MemberId, command.Position);
        return Unit.Value;
    }
}
