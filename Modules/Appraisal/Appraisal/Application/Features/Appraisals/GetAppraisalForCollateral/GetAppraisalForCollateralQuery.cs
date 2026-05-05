using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalForCollateral;

/// <summary>
/// Query used by the Collateral module (in-process MediatR) to retrieve all appraisal data
/// needed for CollateralMaster upsert.
/// Returns null when the appraisal does not exist — consumer dead-letters on null.
/// </summary>
public record GetAppraisalForCollateralQuery(Guid AppraisalId)
    : IQuery<AppraisalForCollateralResult?>;
