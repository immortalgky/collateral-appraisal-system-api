using Workflow.Domain.Committees;
using Workflow.Workflow.Engine.Expression;

namespace Workflow.Workflow.Activities.Approval;

public class ApprovalMemberResolver(
    ICommitteeRepository committeeRepository,
    ILogger<ApprovalMemberResolver> logger
) : IApprovalMemberResolver
{
    private readonly ExpressionEvaluator _expressionEvaluator = new();

    public async Task<ApprovalGroupInfo> ResolveMembersAsync(
        MemberSourceConfig config,
        Dictionary<string, object> variables,
        QuorumConfig? inlineQuorum,
        MajorityConfig? inlineMajority,
        CancellationToken ct = default)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "inline" => ResolveInline(config, inlineQuorum, inlineMajority),
            "committee" => await ResolveFromCommittee(config, ct),
            "threshold" => await ResolveFromThreshold(config, variables, ct),
            _ => throw new InvalidOperationException($"Unknown memberSource type: {config.Type}")
        };
    }

    private static ApprovalGroupInfo ResolveInline(MemberSourceConfig config,
        QuorumConfig? quorum, MajorityConfig? majority)
    {
        if (config.Members is null || config.Members.Count == 0)
            throw new InvalidOperationException("Inline memberSource requires at least one member");

        var members = config.Members
            .Select(m => new ApprovalMemberInfo(m, null))
            .ToList();

        return new ApprovalGroupInfo(
            members,
            quorum ?? new QuorumConfig("Fixed", members.Count),
            majority ?? new MajorityConfig("Simple", "approve"),
            new List<ApprovalConditionInfo>(),
            null,
            null);
    }

    private async Task<ApprovalGroupInfo> ResolveFromCommittee(MemberSourceConfig config, CancellationToken ct)
    {
        Committee? committee = null;

        if (config.CommitteeId.HasValue)
            committee = await committeeRepository.GetByIdWithMembersAsync(config.CommitteeId.Value, ct);
        else if (!string.IsNullOrEmpty(config.CommitteeCode))
            committee = await committeeRepository.GetByCodeAsync(config.CommitteeCode, ct);

        if (committee is null)
            throw new NotFoundException(
                $"Committee not found (code={config.CommitteeCode}, id={config.CommitteeId})");

        return MapCommitteeToGroupInfo(committee);
    }

    private async Task<ApprovalGroupInfo> ResolveFromThreshold(
        MemberSourceConfig config, Dictionary<string, object> variables, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(config.ValueExpression))
            throw new InvalidOperationException("Threshold memberSource requires valueExpression");

        if (config.Thresholds is null || config.Thresholds.Count == 0)
            throw new InvalidOperationException("Threshold memberSource requires at least one threshold");

        // Evaluate the value expression from workflow variables
        decimal value;
        if (variables.TryGetValue(config.ValueExpression, out var rawValue))
        {
            value = Convert.ToDecimal(rawValue);
        }
        else
        {
            // Try expression evaluation
            try
            {
                var result = _expressionEvaluator.EvaluateExpression(config.ValueExpression, variables);
                value = Convert.ToDecimal(result);
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Could not evaluate valueExpression '{config.ValueExpression}' from workflow variables");
            }
        }

        logger.LogInformation("Threshold resolution: value={Value} from expression '{Expression}'",
            value, config.ValueExpression);

        // Match threshold — sorted by maxValue ascending, null maxValue means unlimited
        var matchedThreshold = config.Thresholds
            .OrderBy(t => t.MaxValue ?? decimal.MaxValue)
            .FirstOrDefault(t => t.MaxValue is null || value <= t.MaxValue);

        if (matchedThreshold is null)
            throw new InvalidOperationException(
                $"No threshold matched for value {value}");

        var committee = await committeeRepository.GetByCodeAsync(matchedThreshold.CommitteeCode, ct)
            ?? throw new NotFoundException(
                $"Committee with code '{matchedThreshold.CommitteeCode}' not found for threshold match");

        logger.LogInformation("Threshold matched committee '{CommitteeCode}' for value {Value}",
            committee.Code, value);

        return MapCommitteeToGroupInfo(committee);
    }

    private static ApprovalGroupInfo MapCommitteeToGroupInfo(Committee committee)
    {
        var activeMembers = committee.GetActiveMembers();
        if (activeMembers.Count == 0)
            throw new InvalidOperationException($"Committee '{committee.Code}' has no active members");

        var members = activeMembers
            .Select(m => new ApprovalMemberInfo(m.UserId, m.Role.ToString()))
            .ToList();

        var quorum = new QuorumConfig(committee.QuorumType.ToString(), committee.QuorumValue);
        var majority = new MajorityConfig(committee.MajorityType.ToString(), "approve");

        var conditions = committee.Conditions
            .Where(c => c.IsActive)
            .OrderBy(c => c.Priority)
            .Select(c => new ApprovalConditionInfo(
                c.ConditionType.ToString(), c.RoleRequired, c.MinVotesRequired))
            .ToList();

        return new ApprovalGroupInfo(members, quorum, majority, conditions,
            committee.Name, committee.Code);
    }
}
