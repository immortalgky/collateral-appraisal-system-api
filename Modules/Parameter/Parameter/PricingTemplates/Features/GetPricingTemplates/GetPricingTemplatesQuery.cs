namespace Parameter.PricingTemplates.Features.GetPricingTemplates;

public record GetPricingTemplatesQuery(bool ActiveOnly) : IQuery<GetPricingTemplatesResult>;
