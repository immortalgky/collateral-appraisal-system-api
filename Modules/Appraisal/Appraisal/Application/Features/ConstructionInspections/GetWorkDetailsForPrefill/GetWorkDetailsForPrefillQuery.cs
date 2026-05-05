using Shared.CQRS;

namespace Appraisal.Application.Features.ConstructionInspections.GetWorkDetailsForPrefill;

/// <summary>
/// Returns the work details of a prior ConstructionInspection so the FE can seed
/// the new inspection's PreviousProgressPct per item (Progressive appraisal prefill).
/// </summary>
public record GetWorkDetailsForPrefillQuery(Guid InspectionId)
    : IQuery<GetWorkDetailsForPrefillResult>;
