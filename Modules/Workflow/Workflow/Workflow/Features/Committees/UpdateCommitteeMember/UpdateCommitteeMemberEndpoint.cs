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

        if (!Enum.TryParse<CommitteeMemberPosition>(req.Role, ignoreCase: true, out var position))
            throw new ArgumentException(
                $"Invalid Role '{req.Role}'. Allowed values: {string.Join(", ", Enum.GetNames<CommitteeMemberPosition>())}");

        if (!Enum.TryParse<CommitteeAttendance>(req.Attendance, ignoreCase: true, out var attendance))
            throw new ArgumentException(
                $"Invalid Attendance '{req.Attendance}'. Allowed values: {string.Join(", ", Enum.GetNames<CommitteeAttendance>())}");

        committee.UpdateMember(command.MemberId, position, attendance, req.IsActive);

        await committeeRepository.UpdateAsync(committee, ct);

        return Unit.Value;
    }
}
