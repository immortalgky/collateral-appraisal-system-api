namespace Appraisal.Application.Features.MarketComparableTemplates.AddFactorToTemplate;

public record AddFactorToTemplateRequest(
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory
);
