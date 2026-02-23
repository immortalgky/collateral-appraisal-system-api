namespace Appraisal.Application.Features.Appraisals.SaveLawAndRegulations;

public record SaveLawAndRegulationsResponse(
    Guid AppraisalId,
    int ItemCount,
    bool Success
);
