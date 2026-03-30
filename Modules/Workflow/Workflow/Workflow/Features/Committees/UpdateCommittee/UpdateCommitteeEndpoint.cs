using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.UpdateCommittee;

public class UpdateCommitteeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/workflows/committees/{id:guid}", async (
                Guid id,
                UpdateCommitteeRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new UpdateCommitteeCommand(id, request);
                var result = await sender.Send(command, ct);
                return Results.Ok(result);
            })
            .WithName("UpdateCommittee")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces<UpdateCommitteeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record UpdateCommitteeRequest(
    string Name,
    string? Description,
    string QuorumType,
    int QuorumValue,
    string MajorityType,
    bool IsActive);

public record UpdateCommitteeCommand(Guid Id, UpdateCommitteeRequest Request) : ICommand<UpdateCommitteeResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record UpdateCommitteeResponse(Guid Id, string Name);

public class UpdateCommitteeCommandHandler(
    ICommitteeRepository committeeRepository
) : ICommandHandler<UpdateCommitteeCommand, UpdateCommitteeResponse>
{
    public async Task<UpdateCommitteeResponse> Handle(UpdateCommitteeCommand command, CancellationToken ct)
    {
        var committee = await committeeRepository.GetByIdWithMembersAsync(command.Id, ct)
            ?? throw new NotFoundException($"Committee {command.Id} not found");

        var req = command.Request;
        if (!Enum.TryParse<QuorumType>(req.QuorumType, ignoreCase: true, out var quorumType))
            throw new ArgumentException($"Invalid QuorumType '{req.QuorumType}'. Allowed values: {string.Join(", ", Enum.GetNames<QuorumType>())}");
        if (!Enum.TryParse<MajorityType>(req.MajorityType, ignoreCase: true, out var majorityType))
            throw new ArgumentException($"Invalid MajorityType '{req.MajorityType}'. Allowed values: {string.Join(", ", Enum.GetNames<MajorityType>())}");


        committee.Update(req.Name, req.Description, quorumType, req.QuorumValue, majorityType, req.IsActive);

        await committeeRepository.UpdateAsync(committee, ct);

        return new UpdateCommitteeResponse(committee.Id, committee.Name);
    }
}
