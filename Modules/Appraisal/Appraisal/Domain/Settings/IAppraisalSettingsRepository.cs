namespace Appraisal.Domain.Settings;

/// <summary>
/// Repository interface for AppraisalSettings and AutoAssignmentRule.
/// </summary>
public interface IAppraisalSettingsRepository : IRepository<AppraisalSettings, Guid>
{
    Task<AppraisalSettings?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalSettings>> GetAllSettingsAsync(CancellationToken cancellationToken = default);
}

public interface IAutoAssignmentRuleRepository : IRepository<AutoAssignmentRule, Guid>
{
    Task<IEnumerable<AutoAssignmentRule>> GetActiveRulesOrderedAsync(CancellationToken cancellationToken = default);

    Task<AutoAssignmentRule?> FindMatchingRuleAsync(
        string propertyType,
        string province,
        decimal estimatedValue,
        string? loanType,
        string priority,
        CancellationToken cancellationToken = default);
}