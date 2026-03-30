using Auth.Domain.Companies;

namespace Auth.Application.Features.Companies.GetCompanies;

public class GetCompaniesQueryHandler(ICompanyRepository companyRepository)
    : IQueryHandler<GetCompaniesQuery, GetCompaniesResult>
{
    public async Task<GetCompaniesResult> Handle(
        GetCompaniesQuery query,
        CancellationToken cancellationToken)
    {
        var companies = string.IsNullOrWhiteSpace(query.Search)
            ? await companyRepository.GetAllAsync(query.ActiveOnly, cancellationToken)
            : await companyRepository.SearchAsync(query.Search, query.ActiveOnly, cancellationToken);

        var dtos = companies.Select(c => new CompanyDto(
            c.Id, c.Name, c.TaxId, c.Phone, c.Email,
            c.Street, c.City, c.Province, c.PostalCode, c.ContactPerson, c.IsActive, c.LoanTypes
        )).ToList();

        return new GetCompaniesResult(dtos);
    }
}
