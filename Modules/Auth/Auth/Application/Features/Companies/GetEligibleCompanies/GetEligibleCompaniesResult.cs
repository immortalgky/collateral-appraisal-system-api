namespace Auth.Application.Features.Companies.GetEligibleCompanies;

public record GetEligibleCompaniesResult(List<EligibleCompanyDto> Companies);

public record EligibleCompanyDto(
    Guid    Id,
    string  Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? TaxId,
    decimal AverageRating,
    int     EvaluationCount,
    int     ActiveAssignments,
    // Advisory: false when the company is outside its MOU approval window. The picker is intentionally
    // unfiltered (shared with user-account association), so the FE can use this to badge/disable rows.
    bool    IsAssignable = true);
