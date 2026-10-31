namespace Auth.Contracts.Companies;

/// <summary>
/// Cross-module snapshot of an external appraisal company.
/// <paramref name="IsAssignable"/> is the resolved answer to "may the bank engage this company
/// right now" — it already folds in IsActive, IsDeleted and the MOU effective/expire window, so
/// consumers never re-implement that rule.
/// <paramref name="Email"/> is the company's one address, where the bank emails assigned work.
/// </summary>
public record CompanyLookupDto(
    Guid Id,
    string Name,
    string? LegacyCompanyCode,
    bool IsAssignable,
    string? Email = null,
    string? NameLocal = null);
