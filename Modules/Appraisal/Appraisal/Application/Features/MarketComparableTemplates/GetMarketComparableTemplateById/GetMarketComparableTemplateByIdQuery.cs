using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.GetMarketComparableTemplateById;

public record GetMarketComparableTemplateByIdQuery(Guid Id) : IQuery<GetMarketComparableTemplateByIdResult>;
