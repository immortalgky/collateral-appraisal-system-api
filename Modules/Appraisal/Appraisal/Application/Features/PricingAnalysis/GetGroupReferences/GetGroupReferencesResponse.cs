using Appraisal.Application.Features.PricingAnalysis.GetReferences;

namespace Appraisal.Application.Features.PricingAnalysis.GetGroupReferences;

public record GetGroupReferencesResponse(IReadOnlyList<ReferenceDto> References);
