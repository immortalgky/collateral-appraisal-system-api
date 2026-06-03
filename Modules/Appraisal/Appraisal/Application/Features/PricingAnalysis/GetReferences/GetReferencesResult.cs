namespace Appraisal.Application.Features.PricingAnalysis.GetReferences;

public record GetReferencesResult(IReadOnlyList<ReferenceDto> References);

public record ReferenceDto(
    Guid PricingAnalysisId,
    PricingAnalysisSubjectType SubjectType,
    Guid AnchorId,
    string? AnchorRefKey,
    Guid? HostMethodId,
    string Status,
    IReadOnlyList<ReferenceMethodDto> Methods
);

public record ReferenceMethodDto(
    Guid MethodId,
    string MethodType,
    decimal? FinalValue,
    decimal? ValuePerUnit
);
