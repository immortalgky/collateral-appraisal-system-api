using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.AddCommitteeCondition;

/// <summary>
/// Approval conditions are extra rules a round must satisfy on top of quorum and the majority rule
/// (see <c>ApprovalActivity.CheckApprovalConditions</c>). Until now they could only be supplied when
/// the committee was first created, which made them seed-only in practice.
///
/// Note the conditions are snapshotted into the workflow instance when a round starts, so a change
/// here affects NEW rounds only — in-flight approvals keep the rules they began with.
/// </summary>
public class AddCommitteeConditionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/committees/{committeeId:guid}/conditions", async (
                Guid committeeId,
                AddCommitteeConditionRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new AddCommitteeConditionCommand(committeeId, request);
                var result = await sender.Send(command, ct);
                return Results.Created(
                    $"/api/workflows/committees/{committeeId}/conditions/{result.Id}", result);
            })
            .WithName("AddCommitteeCondition")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces<CommitteeConditionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record AddCommitteeConditionRequest(
    string ConditionType,
    string? RoleRequired = null,
    int? MinVotesRequired = null,
    int Priority = 1,
    string? Description = null);

public record AddCommitteeConditionCommand(Guid CommitteeId, AddCommitteeConditionRequest Request)
    : ICommand<CommitteeConditionResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CommitteeConditionResponse(
    Guid Id,
    Guid CommitteeId,
    string ConditionType,
    string? RoleRequired,
    int? MinVotesRequired,
    int Priority,
    bool IsActive,
    string? Description);

public class AddCommitteeConditionCommandHandler(
    ICommitteeRepository committeeRepository)
    : ICommandHandler<AddCommitteeConditionCommand, CommitteeConditionResponse>
{
    public async Task<CommitteeConditionResponse> Handle(
        AddCommitteeConditionCommand command, CancellationToken ct)
    {
        // Members must be loaded: the domain rejects a condition no active member could satisfy.
        var committee = await committeeRepository.GetByIdWithMembersAsync(command.CommitteeId, ct)
            ?? throw new NotFoundException($"Committee {command.CommitteeId} not found");

        var req = command.Request;

        if (!Enum.TryParse<ConditionType>(req.ConditionType, ignoreCase: true, out var conditionType))
            throw new BadRequestException(
                $"Invalid ConditionType '{req.ConditionType}'. Allowed values: " +
                $"{string.Join(", ", Enum.GetNames<ConditionType>())}");

        // The domain signals an unsatisfiable condition with ArgumentException, which the global
        // CustomExceptionHandler does not map — translate it here so the caller gets 400, not 500.
        CommitteeApprovalCondition condition;
        try
        {
            condition = committee.AddCondition(
                conditionType, req.RoleRequired, req.MinVotesRequired, req.Priority, req.Description);
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        await committeeRepository.UpdateAsync(committee, ct);

        return new CommitteeConditionResponse(
            condition.Id, committee.Id, condition.ConditionType.ToString(),
            condition.RoleRequired, condition.MinVotesRequired, condition.Priority,
            condition.IsActive, condition.Description);
    }
}
