using Workflow.Services.Configuration.Models;

namespace Workflow.Services.Configuration;

/// <summary>
/// Manages the external-company round-robin pool/weight configuration and resolves the active pool
/// for a given loan-type scope at assignment time.
/// </summary>
public interface ICompanyRoundRobinConfigService
{
    /// <summary>
    /// Resolves the active pool for a loan type: the loan-type-specific pool if one exists, otherwise
    /// the global (LoanType = null) pool. Returns null when no active pool is configured (caller then
    /// falls back to all active companies).
    /// </summary>
    Task<CompanyRoundRobinConfigurationDto?> ResolveAsync(
        string? loanType,
        CancellationToken cancellationToken = default);

    Task<CompanyRoundRobinConfigurationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<CompanyRoundRobinConfigurationDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CompanyRoundRobinConfigurationDto> CreateAsync(
        CreateCompanyRoundRobinConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<CompanyRoundRobinConfigurationDto> UpdateAsync(
        Guid id,
        UpdateCompanyRoundRobinConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
