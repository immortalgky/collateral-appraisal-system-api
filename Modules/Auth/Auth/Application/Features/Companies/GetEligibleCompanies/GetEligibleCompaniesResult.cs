namespace Auth.Application.Features.Companies.GetEligibleCompanies;

public record GetEligibleCompaniesResult(List<EligibleCompanyDto> Companies);

public record EligibleCompanyDto(
    Guid    Id,
    string  Name,
    // Thai name. Nullable — the FE falls back to Name when it is absent. Returned alongside (not
    // instead of) Name because the API has no request locale; the client picks by its own language.
    string? NameLocal,
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
