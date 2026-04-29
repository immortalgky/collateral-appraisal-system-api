namespace Auth.Application.Features.Companies.GetEligibleCompanies;

public record GetEligibleCompaniesQuery(string? LoanType) : IQuery<GetEligibleCompaniesResult>;
