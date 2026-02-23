namespace Appraisal.Application.Features.Appraisals.SaveLawAndRegulations;

public record SaveLawAndRegulationsResult(
    Guid AppraisalId,
    int ItemCount,
    bool Success
);
