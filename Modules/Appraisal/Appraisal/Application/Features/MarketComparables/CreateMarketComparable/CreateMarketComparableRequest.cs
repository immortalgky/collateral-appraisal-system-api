namespace Appraisal.Application.Features.MarketComparables.CreateMarketComparable;

public record CreateMarketComparableRequest(
    string PropertyType,
    string SurveyName,
    DateTime? InfoDateTime = null,
    string? SourceInfo = null,
    string? Notes = null,
    Guid? TemplateId = null
);