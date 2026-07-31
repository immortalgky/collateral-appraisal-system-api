namespace Auth.Contracts.Companies;

/// <summary>
/// Cross-module contract for resolving external appraisal companies by id.
/// Implemented in the Auth module (which owns auth.Companies), consumed by modules that hold a
/// company id and need its name or its assignability — e.g. Appraisal, when recording an
/// engagement the bank arranged outside the system.
/// </summary>
public interface ICompanyLookupService
{
    /// <summary>
    /// Returns the company, or null when no such company exists.
    /// Check <see cref="CompanyLookupDto.IsAssignable"/> before engaging it.
    /// </summary>
    Task<CompanyLookupDto?> GetByIdAsync(Guid companyId, CancellationToken ct = default);
}
