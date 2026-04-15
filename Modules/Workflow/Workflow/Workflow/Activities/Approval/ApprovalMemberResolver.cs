using System.Text.Json;
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
            // Workflow variables round-trip through JSON persistence, so primitive
            // values often come back as JsonElement. Convert.ToDecimal cannot handle
            // JsonElement directly (no IConvertible), so normalize first.
            if (!TryConvertToDecimal(rawValue, out value))
            {
                throw new InvalidOperationException(
                    $"Could not convert value of '{config.ValueExpression}' (type {rawValue?.GetType().Name}) to decimal");
            }
        }
        else
        {
            // Try expression evaluation
            try
            {
                var result = _expressionEvaluator.EvaluateExpression(config.ValueExpression, variables);
                if (!TryConvertToDecimal(result, out value))
                {
                    throw new InvalidOperationException(
                        $"Could not convert expression result of '{config.ValueExpression}' to decimal");
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Could not evaluate valueExpression '{config.ValueExpression}' from workflow variables", ex);
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

    private static bool TryConvertToDecimal(object? value, out decimal result)
    {
        result = 0m;
        if (value is null) return false;

        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Number => je.TryGetDecimal(out result),
                JsonValueKind.String => decimal.TryParse(je.GetString(), out result),
                _ => false
            };
        }

        if (value is IConvertible)
        {
            try { result = Convert.ToDecimal(value); return true; }
            catch { return false; }
        }

        return decimal.TryParse(value.ToString(), out result);
    }

    private static ApprovalGroupInfo MapCommitteeToGroupInfo(Committee committee)
    {
        var activeMembers = committee.GetActiveMembers();
        if (activeMembers.Count == 0)
            throw new InvalidOperationException($"Committee '{committee.Code}' has no active members");

        var members = activeMembers
            .Select(m => new ApprovalMemberInfo(m.UserId, m.Position.ToString()))
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
