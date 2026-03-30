namespace Auth.Application.Features.Companies.GetEligibleCompanies;

public record GetEligibleCompaniesResult(List<EligibleCompanyDto> Companies);

public record EligibleCompanyDto(Guid Id, string Name, string? ContactPerson, string? Phone, string? Email, string? TaxId);
