using Shared.DDD;

namespace Workflow.Data.Entities;

/// <summary>
/// Admin-configurable pool + weights for the external-company round-robin. Restricts auto-assignment
/// to a chosen subset of companies and gives each a relative weight in the rotation. When no active
/// configuration exists for a scope, the round-robin falls back to all active companies (weight 1).
/// </summary>
public class CompanyRoundRobinConfiguration : Entity<Guid>
{
    /// <summary>
    /// Loan-type scope this pool applies to (matches the round-robin group key, e.g. "Retail"/"IBG").
    /// Null = global/default pool used when no loan-type-specific pool is active.
    /// </summary>
    public string? LoanType { get; private set; }

    /// <summary>
    /// JSON array of <c>{ companyId, weight }</c> entries — the companies in the pool and their weights.
    /// Presence in the list = in the pool; weight is a positive integer (defaults to 1).
    /// </summary>
    public string Entries { get; private set; } = default!;

    /// <summary>Whether this configuration is active.</summary>
    public bool IsActive { get; private set; }

    public new DateTime CreatedAt { get; private set; }
    public new DateTime UpdatedAt { get; private set; }
    public new string CreatedBy { get; private set; } = default!;
    public new string UpdatedBy { get; private set; } = default!;

    private CompanyRoundRobinConfiguration()
    {
        // For EF Core
    }

    public static CompanyRoundRobinConfiguration Create(
        string entries,
        string createdBy,
        string? loanType = null,
        bool isActive = true)
    {
        return new CompanyRoundRobinConfiguration
        {
            LoanType = loanType,
            Entries = entries,
            IsActive = isActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };
    }

    public void Update(
        string entries,
        string updatedBy,
        string? loanType = null,
        bool isActive = true)
    {
        Entries = entries;
        LoanType = loanType;
        IsActive = isActive;
        UpdatedAt = DateTime.Now;
        UpdatedBy = updatedBy;
    }
}
