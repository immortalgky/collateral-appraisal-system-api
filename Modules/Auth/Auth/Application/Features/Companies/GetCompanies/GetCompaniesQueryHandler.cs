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

        // Count users belonging to each company on this page.
        const string userCountSql =
            "SELECT CompanyId, COUNT(*) AS UserCount FROM auth.AspNetUsers " +
            "WHERE CompanyId IN @ids GROUP BY CompanyId";
        var userCounts = (await connection.QueryAsync<CompanyUserCountRow>(userCountSql, new { ids }))
            .ToDictionary(r => r.CompanyId, r => r.UserCount);

        var dtos = companies.Select(c =>
        {
            overviewMap.TryGetValue(c.Id, out var ov);
            return new CompanyDto(
                c.Id, c.Name, c.NameLocal, c.TaxId, c.Phone, c.Email,
                c.AddressLine1, c.AddressLine2, c.EffectiveDate, c.ExpireDate,
                c.ContactPerson, c.IsActive, c.LoanTypes,
                HostCompanyCode:  c.HostCompanyCode,
                BankAccountNo:    c.BankAccountNo,
                BankAccountName:  c.BankAccountName,
                AverageRating:    ov?.AverageRating    ?? 0m,
                EvaluationCount:  ov?.EvaluationCount  ?? 0,
                ActiveAssignments: ov?.ActiveAssignments ?? 0,
                UserCount:        userCounts.GetValueOrDefault(c.Id));
        }).ToList();

        return new GetCompaniesResult(dtos);
    }

    private sealed record CompanyUserCountRow(Guid CompanyId, int UserCount);
}
