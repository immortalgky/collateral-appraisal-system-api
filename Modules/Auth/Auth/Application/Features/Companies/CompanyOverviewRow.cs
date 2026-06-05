namespace Auth.Application.Features.Companies.GetEligibleCompanies;

/// <summary>
/// Dapper result row from common.vw_CompanyOverview.
/// Used by GetEligibleCompanies, GetCompanyById, and GetCompanies handlers
/// to populate quality-metric fields on company DTOs.
/// </summary>
public record CompanyOverviewRow(
    Guid    CompanyId,
    decimal AverageRating,
    int     EvaluationCount,
    int     ActiveAssignments);
