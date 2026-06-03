using Appraisal.Application.Features.PricingAnalysis.GetReferences;

namespace Appraisal.Application.Features.PricingAnalysis.GetGroupReferences;

// Reuses the shared ReferenceDto / ReferenceMethodDto from the GetReferences feature.
public record GetGroupReferencesResult(IReadOnlyList<ReferenceDto> References);
