using Auth.Domain.Companies;
using Dapper;
using Shared.Time;

namespace Auth.Application.Features.Companies.GetEligibleCompanies;

public class GetEligibleCompaniesQueryHandler(
    ICompanyRepository companyRepository,
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetEligibleCompaniesQuery, GetEligibleCompaniesResult>
{
    public async Task<GetEligibleCompaniesResult> Handle(
        GetEligibleCompaniesQuery query,
        CancellationToken cancellationToken)
    {
        var companies = string.IsNullOrWhiteSpace(query.LoanType)
            ? await companyRepository.GetAllAsync(activeOnly: true, cancellationToken)
            : await companyRepository.GetByLoanTypeAsync(query.LoanType, activeOnly: true, cancellationToken);

        // NOTE: deliberately NOT filtered by the MOU approval window. This endpoint is shared (e.g. the
        // user-management company dropdown for account association), so MOU enforcement lives at the
        // assignment surfaces (round-robin selection + CompanySelectionActivity), not here.
        if (!companies.Any())
            return new GetEligibleCompaniesResult([]);

        // Batch-read quality metrics from the company overview view
        var ids = companies.Select(c => c.Id).ToList();
        var overviewMap = await LoadOverviewAsync(ids);

        var now = dateTimeProvider.ApplicationNow;
        var dtos = companies.Select(c =>
        {
            overviewMap.TryGetValue(c.Id, out var ov);
            return new EligibleCompanyDto(
                c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.TaxId,
                AverageRating:    ov?.AverageRating    ?? 0m,
                EvaluationCount:  ov?.EvaluationCount  ?? 0,
                ActiveAssignments: ov?.ActiveAssignments ?? 0,
                IsAssignable:     c.IsAssignable(now));
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
