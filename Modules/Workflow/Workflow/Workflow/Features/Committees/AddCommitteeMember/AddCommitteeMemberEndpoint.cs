using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.AddCommitteeMember;

public class AddCommitteeMemberEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/committees/{id:guid}/members", async (
                Guid id,
                AddCommitteeMemberRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new AddCommitteeMemberCommand(id, request);
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/workflows/committees/{id}/members/{result.Id}", result);
            })
            .WithName("AddCommitteeMember")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces<AddCommitteeMemberResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record AddCommitteeMemberRequest(
    string UserId,
    string MemberName,
    string Role,
    string? Attendance = null);

public record AddCommitteeMemberCommand(Guid CommitteeId, AddCommitteeMemberRequest Request)
    : ICommand<AddCommitteeMemberResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record AddCommitteeMemberResponse(
    Guid Id,
    Guid CommitteeId,
    string UserId,
    string MemberName,
    string Role,
    string Attendance,
    bool IsActive);

public class AddCommitteeMemberCommandHandler(
    ICommitteeRepository committeeRepository)
    : ICommandHandler<AddCommitteeMemberCommand, AddCommitteeMemberResponse>
{
    public async Task<AddCommitteeMemberResponse> Handle(AddCommitteeMemberCommand command, CancellationToken ct)
    {
        var committee = await committeeRepository.GetByIdWithMembersAsync(command.CommitteeId, ct)
            ?? throw new NotFoundException($"Committee {command.CommitteeId} not found");

        var req = command.Request;

        if (!Enum.TryParse<CommitteeMemberPosition>(req.Role, ignoreCase: true, out var position))
            throw new ArgumentException(
                $"Invalid Role '{req.Role}'. Allowed values: {string.Join(", ", Enum.GetNames<CommitteeMemberPosition>())}");

        var attendance = string.IsNullOrWhiteSpace(req.Attendance)
            ? CommitteeAttendance.Always
            : Enum.Parse<CommitteeAttendance>(req.Attendance, ignoreCase: true);

        var member = committee.AddMember(req.UserId, req.MemberName, position, attendance);

        await committeeRepository.UpdateAsync(committee, ct);

        return new AddCommitteeMemberResponse(
            member.Id, committee.Id, member.UserId, member.MemberName,
            member.Position.ToString(), member.Attendance.ToString(), member.IsActive);
    }
}
