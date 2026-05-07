using MediatR;

namespace Collateral.Contracts.ConstructionInspection;

/// <summary>
/// Looks up the most recent CollateralEngagement's ConstructionInspectionFeeAmount
/// for a given prior appraisal ID. Used by the Appraisal module's AssignmentFeeService
/// to seed the appraisal fee on a new Construction Inspection appraisal — CI bypasses
/// the normal tier/quotation pipeline and reuses the CI fee captured on the prior engagement.
///
/// Returns null when no engagement carries a non-null CI fee.
/// </summary>
public record GetConstructionInspectionFeeForAppraisalQuery(Guid AppraisalId)
    : IRequest<decimal?>;
