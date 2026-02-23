namespace Appraisal.Application.Features.MarketComparableTemplates.UpdateMarketComparableTemplate;

public record UpdateMarketComparableTemplateRequest(
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description
);
