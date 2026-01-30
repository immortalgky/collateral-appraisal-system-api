using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.GetMarketComparableTemplates;

public record GetMarketComparableTemplatesQuery(
    string? PropertyType = null,
    bool? IsActive = null
) : IQuery<GetMarketComparableTemplatesResult>;
