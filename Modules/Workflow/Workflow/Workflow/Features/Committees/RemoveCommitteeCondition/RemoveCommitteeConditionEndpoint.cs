using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.RemoveCommitteeCondition;

/// <summary>
/// Soft-removes the condition (deactivates it), mirroring <c>RemoveCommitteeMember</c>. Approval
/// history and the instance snapshots still reference the row, so it is never hard-deleted.
/// </summary>
public class RemoveCommitteeConditionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/api/workflows/committees/{committeeId:guid}/conditions/{conditionId:guid}",
                async (
                    Guid committeeId,
                    Guid conditionId,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    await sender.Send(new RemoveCommitteeConditionCommand(committeeId, conditionId), ct);
                    return Results.NoContent();
                })
            .WithName("RemoveCommitteeCondition")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record RemoveCommitteeConditionCommand(Guid CommitteeId, Guid ConditionId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class RemoveCommitteeConditionCommandHandler(
    ICommitteeRepository committeeRepository)
    : ICommandHandler<RemoveCommitteeConditionCommand>
{
    public async Task<Unit> Handle(RemoveCommitteeConditionCommand command, CancellationToken ct)
    {
        var committee = await committeeRepository.GetByIdWithMembersAsync(command.CommitteeId, ct)
            ?? throw new NotFoundException($"Committee {command.CommitteeId} not found");

        committee.RemoveCondition(command.ConditionId);

        await committeeRepository.UpdateAsync(committee, ct);

        return Unit.Value;
    }
}
