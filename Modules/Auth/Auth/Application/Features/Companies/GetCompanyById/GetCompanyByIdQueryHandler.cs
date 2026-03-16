using Auth.Application.Features.Companies.GetCompanies;
using Auth.Domain.Companies;
using Shared.Exceptions;

namespace Auth.Application.Features.Companies.GetCompanyById;

public class GetCompanyByIdQueryHandler(ICompanyRepository companyRepository)
    : IQueryHandler<GetCompanyByIdQuery, GetCompanyByIdResult>
{
    public async Task<GetCompanyByIdResult> Handle(
        GetCompanyByIdQuery query,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException("Company", query.Id);

        var dto = new CompanyDto(
            company.Id, company.Name, company.TaxId, company.Phone, company.Email,
            company.Street, company.City, company.Province, company.PostalCode, company.IsActive);

        return new GetCompanyByIdResult(dto);
    }
}
