using Auth.Application.Features.Companies.GetCompanies;
using Auth.Application.Features.Companies.GetEligibleCompanies;
using Auth.Domain.Companies;
using Dapper;
using Shared.Exceptions;

namespace Auth.Application.Features.Companies.GetCompanyById;

public class GetCompanyByIdQueryHandler(
    ICompanyRepository companyRepository,
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetCompanyByIdQuery, GetCompanyByIdResult>
{
    public async Task<GetCompanyByIdResult> Handle(
        GetCompanyByIdQuery query,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException("Company", query.Id);

        // Read quality metrics from the company overview view
        const string sql =
            "SELECT CompanyId, AverageRating, EvaluationCount, ActiveAssignments " +
            "FROM common.vw_CompanyOverview " +
            "WHERE CompanyId = @id";

        var connection = connectionFactory.GetOpenConnection();
        var ov = await connection.QuerySingleOrDefaultAsync<CompanyOverviewRow>(
            sql, new { id = company.Id });

        var dto = new CompanyDto(
            company.Id, company.Name, company.TaxId, company.Phone, company.Email,
            company.Street, company.City, company.Province, company.PostalCode,
            company.ContactPerson, company.IsActive, company.LoanTypes,
            HostCompanyCode:  company.HostCompanyCode,
            BankAccountNo:    company.BankAccountNo,
            BankAccountName:  company.BankAccountName,
            AverageRating:    ov?.AverageRating    ?? 0m,
            EvaluationCount:  ov?.EvaluationCount  ?? 0,
            ActiveAssignments: ov?.ActiveAssignments ?? 0);

        return new GetCompanyByIdResult(dto);
    }
}
