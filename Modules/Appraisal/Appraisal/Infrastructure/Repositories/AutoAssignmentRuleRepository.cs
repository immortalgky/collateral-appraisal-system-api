namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for AutoAssignmentRule entity.
/// </summary>
public class AutoAssignmentRuleRepository(AppraisalDbContext dbContext)
    : BaseRepository<AutoAssignmentRule, Guid>(dbContext), IAutoAssignmentRuleRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<IEnumerable<AutoAssignmentRule>> GetActiveRulesOrderedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AutoAssignmentRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AutoAssignmentRule?> FindMatchingRuleAsync(
        string propertyType,
        string province,
        decimal estimatedValue,
        string? loanType,
        string priority,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetActiveRulesOrderedAsync(cancellationToken);

        foreach (var rule in rules)
            if (MatchesRule(rule, propertyType, province, estimatedValue, loanType, priority))
                return rule;

        return null;
    }

    private static bool MatchesRule(
        AutoAssignmentRule rule,
        string propertyType,
        string province,
        decimal estimatedValue,
        string? loanType,
        string priority)
    {
        // Check PropertyTypes (null means all)
        if (!string.IsNullOrEmpty(rule.PropertyTypes))
        {
            var types = rule.PropertyTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!types.Contains(propertyType, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        // Check Provinces (null means all)
        if (!string.IsNullOrEmpty(rule.Provinces))
        {
            var provinces = rule.Provinces.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!provinces.Contains(province, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        // Check value range
        if (rule.MinEstimatedValue.HasValue && estimatedValue < rule.MinEstimatedValue.Value)
            return false;

        if (rule.MaxEstimatedValue.HasValue && estimatedValue > rule.MaxEstimatedValue.Value)
            return false;

        // Check LoanTypes (null means all)
        if (!string.IsNullOrEmpty(rule.LoanTypes) && !string.IsNullOrEmpty(loanType))
        {
            var types = rule.LoanTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!types.Contains(loanType, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        // Check Priorities (null means all)
        if (!string.IsNullOrEmpty(rule.Priorities))
        {
            var priorities = rule.Priorities.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!priorities.Contains(priority, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}