using Auth.Contracts.Companies;
using Auth.Domain.Companies;
using Shared.Time;

namespace Auth.Application.Services;

/// <summary>
/// Implements <see cref="ICompanyLookupService"/> over the Companies repository.
/// Resolves IsAssignable here — via <see cref="Company.IsAssignable"/> — so the MOU-window rule
/// stays owned by the Auth module and consuming modules cannot drift from it.
/// </summary>
public class CompanyLookupService(
    ICompanyRepository companyRepository,
    IDateTimeProvider dateTimeProvider) : ICompanyLookupService
{
    public async Task<CompanyLookupDto?> GetByIdAsync(Guid companyId, CancellationToken ct = default)
    {
        var company = await companyRepository.GetByIdAsync(companyId, ct);

        if (company is null) return null;

        return new CompanyLookupDto(
            company.Id,
            company.Name,
            company.LegacyCompanyCode,
            company.IsAssignable(dateTimeProvider.ApplicationNow));
    }
}
