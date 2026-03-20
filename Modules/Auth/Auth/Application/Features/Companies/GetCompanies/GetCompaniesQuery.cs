namespace Auth.Application.Features.Companies.GetCompanies;

public record GetCompaniesQuery(string? Search, bool ActiveOnly = false) : IQuery<GetCompaniesResult>;
