using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.UpdateCommitteeMember;

public class UpdateCommitteeMemberEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapMethods("/api/workflows/committees/{committeeId:guid}/members/{memberId:guid}", ["PATCH"], async (
                Guid committeeId,
                Guid memberId,
                UpdateCommitteeMemberRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new UpdateCommitteeMemberCommand(committeeId, memberId, request), ct);
                return Results.NoContent();
            })
            .WithName("UpdateCommitteeMember")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record UpdateCommitteeMemberRequest(string Role, string Attendance, bool IsActive);

public record UpdateCommitteeMemberCommand(Guid CommitteeId, Guid MemberId, UpdateCommitteeMemberRequest Request)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class UpdateCommitteeMemberCommandHandler(
    ICommitteeRepository committeeRepository)
    : ICommandHandler<UpdateCommitteeMemberCommand>
{
    public async Task<Unit> Handle(UpdateCommitteeMemberCommand command, CancellationToken ct)
    {
        var committee = await committeeRepository.GetByIdWithMembersAsync(command.CommitteeId, ct)
            ?? throw new NotFoundException($"Committee {command.CommitteeId} not found");

        var req = command.Request;

        // Includes inactive members — reactivating one is exactly the case that must keep working.
        var member = committee.Members.FirstOrDefault(m => m.Id == command.MemberId)
            ?? throw new NotFoundException($"CommitteeMember {command.MemberId} not found");

        if (!CommitteeMemberPositions.TryParseName(req.Role, out var position))
            throw new BadRequestException(
                $"Invalid Role '{req.Role}'. Allowed values: {CommitteeMemberPositions.SelectableNames}");

        // This is a whole-record update, so deactivating or re-scheduling a member added before
        // Risk/Appraisal/Credit/Member were retired still sends their existing role back. Only a
        // CHANGE has to land on a currently-assignable position; an unchanged one may stand.
        if (position != member.Position && !CommitteeMemberPositions.Selectable.Contains(position))
            throw new BadRequestException(
                $"Role '{position}' is retired and can no longer be assigned. " +
                $"Allowed values: {CommitteeMemberPositions.SelectableNames}");

        if (!Enum.TryParse<CommitteeAttendance>(req.Attendance, ignoreCase: true, out var attendance))
            throw new BadRequestException(
                $"Invalid Attendance '{req.Attendance}'. Allowed values: {string.Join(", ", Enum.GetNames<CommitteeAttendance>())}");

        committee.UpdateMember(command.MemberId, position, attendance, req.IsActive);

        await committeeRepository.UpdateAsync(committee, ct);

        return Unit.Value;
    }
}
