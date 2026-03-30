using Auth.Domain.Companies;

namespace Auth.Application.Features.Companies.GetEligibleCompanies;

public class GetEligibleCompaniesQueryHandler(ICompanyRepository companyRepository)
    : IQueryHandler<GetEligibleCompaniesQuery, GetEligibleCompaniesResult>
{
    public async Task<GetEligibleCompaniesResult> Handle(
        GetEligibleCompaniesQuery query,
        CancellationToken cancellationToken)
    {
        var companies = await companyRepository.GetByLoanTypeAsync(query.LoanType, activeOnly: true, cancellationToken);

        var dtos = companies.Select(c => new EligibleCompanyDto(c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.TaxId)).ToList();

        return new GetEligibleCompaniesResult(dtos);
    }
}
