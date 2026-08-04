using Workflow.Domain.Committees;
using Workflow.Workflow.Features.Committees.AddCommitteeCondition;

namespace Workflow.Workflow.Features.Committees.UpdateCommitteeCondition;

/// <summary>
/// PATCH, matching <c>UpdateCommitteeMember</c>. Setting <c>IsActive = false</c> is the normal way
/// to retire a condition — an inactive condition is skipped by the evaluator, so it stops blocking
/// rounds without losing the row.
/// </summary>
public class UpdateCommitteeConditionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapMethods(
                "/api/workflows/committees/{committeeId:guid}/conditions/{conditionId:guid}",
                ["PATCH"],
                async (
                    Guid committeeId,
                    Guid conditionId,
                    UpdateCommitteeConditionRequest request,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var command = new UpdateCommitteeConditionCommand(committeeId, conditionId, request);
                    var result = await sender.Send(command, ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateCommitteeCondition")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces<CommitteeConditionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record UpdateCommitteeConditionRequest(
    string ConditionType,
    string? RoleRequired,
    int? MinVotesRequired,
    int Priority,
    bool IsActive,
    string? Description = null);

public record UpdateCommitteeConditionCommand(
    Guid CommitteeId, Guid ConditionId, UpdateCommitteeConditionRequest Request)
    : ICommand<CommitteeConditionResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public class UpdateCommitteeConditionCommandHandler(
    ICommitteeRepository committeeRepository)
    : ICommandHandler<UpdateCommitteeConditionCommand, CommitteeConditionResponse>
{
    public async Task<CommitteeConditionResponse> Handle(
        UpdateCommitteeConditionCommand command, CancellationToken ct)
    {
        var committee = await committeeRepository.GetByIdWithMembersAsync(command.CommitteeId, ct)
            ?? throw new NotFoundException($"Committee {command.CommitteeId} not found");

        var req = command.Request;

        if (!Enum.TryParse<ConditionType>(req.ConditionType, ignoreCase: true, out var conditionType))
            throw new BadRequestException(
                $"Invalid ConditionType '{req.ConditionType}'. Allowed values: " +
                $"{string.Join(", ", Enum.GetNames<ConditionType>())}");

        // See AddCommitteeCondition: ArgumentException is not mapped globally, so translate to 400.
        try
        {
            committee.UpdateCondition(
                command.ConditionId, conditionType, req.RoleRequired, req.MinVotesRequired,
                req.Priority, req.Description, req.IsActive);
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        await committeeRepository.UpdateAsync(committee, ct);

        var condition = committee.Conditions.First(c => c.Id == command.ConditionId);

        return new CommitteeConditionResponse(
            condition.Id, committee.Id, condition.ConditionType.ToString(),
            condition.RoleRequired, condition.MinVotesRequired, condition.Priority,
            condition.IsActive, condition.Description);
    }
}
