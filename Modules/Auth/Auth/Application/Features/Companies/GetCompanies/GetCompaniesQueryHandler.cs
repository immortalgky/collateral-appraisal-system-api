using Auth.Application.Features.Companies.GetEligibleCompanies;
using Auth.Domain.Companies;
using Dapper;

namespace Auth.Application.Features.Companies.GetCompanies;

public class GetCompaniesQueryHandler(
    ICompanyRepository companyRepository,
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetCompaniesQuery, GetCompaniesResult>
{
    public async Task<GetCompaniesResult> Handle(
        GetCompaniesQuery query,
        CancellationToken cancellationToken)
    {
        var companies = string.IsNullOrWhiteSpace(query.Search)
            ? await companyRepository.GetAllAsync(query.ActiveOnly, cancellationToken)
            : await companyRepository.SearchAsync(query.Search, query.ActiveOnly, cancellationToken);

        if (!companies.Any())
            return new GetCompaniesResult([]);

        // Batch-read quality metrics from the company overview view
        var ids = companies.Select(c => c.Id).ToList();

        const string sql =
            "SELECT CompanyId, AverageRating, EvaluationCount, ActiveAssignments " +
            "FROM common.vw_CompanyOverview " +
            "WHERE CompanyId IN @ids";

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<CompanyOverviewRow>(sql, new { ids });
        var overviewMap = rows.ToDictionary(r => r.CompanyId);

        var dtos = companies.Select(c =>
        {
            overviewMap.TryGetValue(c.Id, out var ov);
            return new CompanyDto(
                c.Id, c.Name, c.TaxId, c.Phone, c.Email,
                c.Street, c.City, c.Province, c.PostalCode,
                c.ContactPerson, c.IsActive, c.LoanTypes,
                BankAccountNo:    c.BankAccountNo,
                BankAccountName:  c.BankAccountName,
                AverageRating:    ov?.AverageRating    ?? 0m,
                EvaluationCount:  ov?.EvaluationCount  ?? 0,
                ActiveAssignments: ov?.ActiveAssignments ?? 0);
        }).ToList();

        return new GetCompaniesResult(dtos);
    }
}
