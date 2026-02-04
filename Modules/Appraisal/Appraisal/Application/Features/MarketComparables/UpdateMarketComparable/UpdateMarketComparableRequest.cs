namespace Appraisal.Application.Features.MarketComparables.UpdateMarketComparable;

public record UpdateMarketComparableRequest(
  string PropertyType,
  string SurveyName,
  DateTime? InfoDateTime = null,
  string? SourceInfo = null,
  string? Notes = null,
  Guid? TemplateId = null
  );
