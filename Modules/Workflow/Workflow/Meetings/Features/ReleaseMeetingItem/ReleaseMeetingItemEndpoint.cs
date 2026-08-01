using Shared.Identity;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.ReleaseMeetingItem;

public class ReleaseMeetingItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/items/{appraisalId:guid}/release", async (
                Guid id,
                Guid appraisalId,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new ReleaseMeetingItemCommand(id, appraisalId), ct);
                return Results.NoContent();
            })
            .WithName("ReleaseMeetingItem")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingSecretary")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record ReleaseMeetingItemCommand(Guid MeetingId, Guid AppraisalId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class ReleaseMeetingItemCommandHandler(
    IMeetingRepository meetingRepository,
    ICommitteeRepository committeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReleaseMeetingItemCommand>
{
    public async Task<Unit> Handle(ReleaseMeetingItemCommand command, CancellationToken ct)
    {
        var actor = currentUserService.Username
            ?? throw new InvalidOperationException("User is not authenticated");

        var meeting = await meetingRepository.GetByIdForDecisionAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        // Releasing hands this roster to the approval activity as its voting members. Refuse now if
        // it cannot satisfy the committee's quorum or approval conditions — otherwise the round
        // opens and silently never resolves.
        var committee = await committeeRepository.GetByCodeAsync(MeetingCommittee.WithMeetingCode, ct)
            ?? throw new NotFoundException($"Committee {MeetingCommittee.WithMeetingCode} not found");

        var failures = MeetingRosterEligibility.Check(meeting.Members, committee);
        if (failures.Count > 0)
            throw new ConflictException(
                $"Meeting roster cannot satisfy committee {committee.Code}: " +
                $"{string.Join("; ", failures)}. Fix the roster before releasing.");

        meeting.ReleaseItem(command.AppraisalId, actor, dateTimeProvider.ApplicationNow);

        return Unit.Value;
    }
}
