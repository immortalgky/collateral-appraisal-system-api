using Auth.Domain.Companies;
using Dapper;

namespace Auth.Application.Features.Companies.GetEligibleCompanies;

public class GetEligibleCompaniesQueryHandler(
    ICompanyRepository companyRepository,
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetEligibleCompaniesQuery, GetEligibleCompaniesResult>
{
    public async Task<GetEligibleCompaniesResult> Handle(
        GetEligibleCompaniesQuery query,
        CancellationToken cancellationToken)
    {
        var companies = string.IsNullOrWhiteSpace(query.LoanType)
            ? await companyRepository.GetAllAsync(activeOnly: true, cancellationToken)
            : await companyRepository.GetByLoanTypeAsync(query.LoanType, activeOnly: true, cancellationToken);

        if (!companies.Any())
            return new GetEligibleCompaniesResult([]);

        // Batch-read quality metrics from the company overview view
        var ids = companies.Select(c => c.Id).ToList();
        var overviewMap = await LoadOverviewAsync(ids);

        var dtos = companies.Select(c =>
        {
            overviewMap.TryGetValue(c.Id, out var ov);
            return new EligibleCompanyDto(
                c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.TaxId,
                AverageRating:    ov?.AverageRating    ?? 0m,
                EvaluationCount:  ov?.EvaluationCount  ?? 0,
                ActiveAssignments: ov?.ActiveAssignments ?? 0);
        }).ToList();

        return new GetEligibleCompaniesResult(dtos);
    }

    private async Task<Dictionary<Guid, CompanyOverviewRow>> LoadOverviewAsync(List<Guid> ids)
    {
        const string sql =
            "SELECT CompanyId, AverageRating, EvaluationCount, ActiveAssignments " +
            "FROM common.vw_CompanyOverview " +
            "WHERE CompanyId IN @ids";

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<CompanyOverviewRow>(sql, new { ids });
        return rows.ToDictionary(r => r.CompanyId);
    }
}
