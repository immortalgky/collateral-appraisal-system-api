using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.CreateCommittee;

public class CreateCommitteeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/committees", async (
                CreateCommitteeRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new CreateCommitteeCommand(request);
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/workflows/committees/{result.Id}", result);
            })
            .WithName("CreateCommittee")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces<CreateCommitteeResponse>(StatusCodes.Status201Created);
    }
}

public record CreateCommitteeRequest(
    string Name,
    string Code,
    string? Description,
    string QuorumType,
    int QuorumValue,
    string MajorityType,
    List<CreateCommitteeMemberRequest>? Members,
    List<CreateCommitteeThresholdRequest>? Thresholds,
    List<CreateCommitteeConditionRequest>? Conditions);

public record CreateCommitteeMemberRequest(string UserId, string MemberName, string Role);
public record CreateCommitteeThresholdRequest(decimal? MinValue, decimal? MaxValue, int Priority);
public record CreateCommitteeConditionRequest(string ConditionType, string? RoleRequired, int? MinVotesRequired, int Priority, string? Description);

public record CreateCommitteeCommand(CreateCommitteeRequest Request) : ICommand<CreateCommitteeResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CreateCommitteeResponse(Guid Id, string Name, string Code);

public class CreateCommitteeCommandHandler(
    ICommitteeRepository committeeRepository
) : ICommandHandler<CreateCommitteeCommand, CreateCommitteeResponse>
{
    public async Task<CreateCommitteeResponse> Handle(CreateCommitteeCommand command, CancellationToken ct)
    {
        var req = command.Request;

        var existing = await committeeRepository.GetByCodeAsync(req.Code, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Committee with code '{req.Code}' already exists");

        if (!Enum.TryParse<QuorumType>(req.QuorumType, ignoreCase: true, out var quorumType))
            throw new ArgumentException($"Invalid QuorumType '{req.QuorumType}'. Allowed values: {string.Join(", ", Enum.GetNames<QuorumType>())}");
        if (!Enum.TryParse<MajorityType>(req.MajorityType, ignoreCase: true, out var majorityType))
            throw new ArgumentException($"Invalid MajorityType '{req.MajorityType}'. Allowed values: {string.Join(", ", Enum.GetNames<MajorityType>())}");


        var committee = Committee.Create(req.Name, req.Code, req.Description, quorumType, req.QuorumValue, majorityType);

        if (req.Members is not null)
        {
            foreach (var m in req.Members)
            {
                var position = Enum.Parse<CommitteeMemberPosition>(m.Role, ignoreCase: true);
                committee.AddMember(m.UserId, m.MemberName, position);
            }
        }

        if (req.Thresholds is not null)
        {
            foreach (var t in req.Thresholds)
                committee.AddThreshold(t.MinValue, t.MaxValue, t.Priority);
        }

        if (req.Conditions is not null)
        {
            foreach (var c in req.Conditions)
            {
                var conditionType = Enum.Parse<ConditionType>(c.ConditionType, ignoreCase: true);
                committee.AddCondition(conditionType, c.RoleRequired, c.MinVotesRequired, c.Priority, c.Description);
            }
        }

        await committeeRepository.AddAsync(committee, ct);

        return new CreateCommitteeResponse(committee.Id, committee.Name, committee.Code);
    }
}
