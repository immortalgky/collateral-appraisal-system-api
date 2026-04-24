using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.RemoveCommitteeMember;

public class RemoveCommitteeMemberEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/workflows/committees/{committeeId:guid}/members/{memberId:guid}", async (
                Guid committeeId,
                Guid memberId,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new RemoveCommitteeMemberCommand(committeeId, memberId), ct);
                return Results.NoContent();
            })
            .WithName("RemoveCommitteeMember")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record RemoveCommitteeMemberCommand(Guid CommitteeId, Guid MemberId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class RemoveCommitteeMemberCommandHandler(
    ICommitteeRepository committeeRepository)
    : ICommandHandler<RemoveCommitteeMemberCommand>
{
    public async Task<Unit> Handle(RemoveCommitteeMemberCommand command, CancellationToken ct)
    {
        var committee = await committeeRepository.GetByIdWithMembersAsync(command.CommitteeId, ct)
            ?? throw new NotFoundException($"Committee {command.CommitteeId} not found");

        committee.RemoveMember(command.MemberId);

        await committeeRepository.UpdateAsync(committee, ct);

        return Unit.Value;
    }
}
