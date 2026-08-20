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
    string? VotingMode,
    List<CreateCommitteeMemberRequest>? Members,
    List<CreateCommitteeThresholdRequest>? Thresholds,
    List<CreateCommitteeConditionRequest>? Conditions,
    int MajorityValue = 0);

public record CreateCommitteeMemberRequest(string UserId, string MemberName, string Role, string? Attendance = null);
public record CreateCommitteeThresholdRequest(decimal? MinValue, decimal? MaxValue, int Priority);
public record CreateCommitteeConditionRequest(string ConditionType, string? RoleRequired, int? MinVotesRequired, int Priority, string? Description);

public record CreateCommitteeCommand(CreateCommitteeRequest Request) : ICommand<CreateCommitteeResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CreateCommitteeResponse(Guid Id, string Name, string Code);

public class CreateCommitteeCommandHandler(
    ICommitteeRepository committeeRepository,
    IUserDirectory userDirectory
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

        var votingMode = VotingMode.WaitForAll;
        if (!string.IsNullOrWhiteSpace(req.VotingMode)
            && !Enum.TryParse(req.VotingMode, ignoreCase: true, out votingMode))
            throw new ArgumentException($"Invalid VotingMode '{req.VotingMode}'. Allowed values: {string.Join(", ", Enum.GetNames<VotingMode>())}");

        var committee = Committee.Create(req.Name, req.Code, req.Description, quorumType,
            req.QuorumValue, majorityType, votingMode, req.MajorityValue);

        if (req.Members is not null)
        {
            // Members are usernames and become approval voters — see AddCommitteeMember.
            var known = await userDirectory.GetExistingAsync(req.Members.Select(m => m.UserId), ct);
            var unknown = req.Members.Select(m => m.UserId).Where(u => !known.Contains(u)).ToList();
            if (unknown.Count > 0)
                throw new BadRequestException(
                    $"No such user(s): {string.Join(", ", unknown)}. Committee members must be existing users");

            foreach (var m in req.Members)
            {
                if (!CommitteeMemberPositions.TryParseSelectable(m.Role, out var position))
                    throw new BadRequestException(
                        $"Invalid Role '{m.Role}'. Allowed values: {CommitteeMemberPositions.SelectableNames}");

                var attendance = CommitteeAttendance.Always;
                if (!string.IsNullOrWhiteSpace(m.Attendance)
                    && !Enum.TryParse(m.Attendance, ignoreCase: true, out attendance))
                    throw new BadRequestException(
                        $"Invalid Attendance '{m.Attendance}'. Allowed values: " +
                        $"{string.Join(", ", Enum.GetNames<CommitteeAttendance>())}");

                committee.AddMember(m.UserId, m.MemberName, position, attendance);
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
                if (!Enum.TryParse<ConditionType>(c.ConditionType, ignoreCase: true, out var conditionType))
                    throw new BadRequestException(
                        $"Invalid ConditionType '{c.ConditionType}'. Allowed values: " +
                        $"{string.Join(", ", Enum.GetNames<ConditionType>())}");

                // The domain signals an unsatisfiable condition with ArgumentException, which the
                // global CustomExceptionHandler does not map — translate it so the caller gets 400,
                // not a 500 with a stack-traced ProblemDetails. Same as AddCommitteeCondition.
                try
                {
                    committee.AddCondition(
                        conditionType, c.RoleRequired, c.MinVotesRequired, c.Priority, c.Description);
                }
                catch (ArgumentException ex)
                {
                    throw new BadRequestException(ex.Message);
                }
            }
        }

        await committeeRepository.AddAsync(committee, ct);

        return new CreateCommitteeResponse(committee.Id, committee.Name, committee.Code);
    }
}
