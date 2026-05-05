using MediatR;

namespace Collateral.Contracts.ConstructionInspection;

/// <summary>
/// Looks up the most recent appraisal company for a given prior appraisal ID.
/// Used by the workflow consumer to seed the `forceCompanyId` variable on workflow start
/// for Construction Inspection requests, ensuring the same company is re-engaged.
///
/// Returns null when no engagement with a company exists for the given appraisal.
/// </summary>
public record GetMostRecentCompanyForAppraisalQuery(Guid AppraisalId)
    : IRequest<(Guid CompanyId, string CompanyName)?>;
