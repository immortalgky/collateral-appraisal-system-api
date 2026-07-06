namespace Appraisal.Application.Features.MarketComparableTemplates.ReorderTemplateFactors;

public record ReorderTemplateFactorsRequest(List<ReorderFactorItem> Factors);

public record ReorderFactorItem(Guid FactorId, int DisplaySequence);
