// The query and result types live in Parameter.Contracts so cross-module callers
// only need a Contracts reference.  Re-export them here for backward compatibility
// within the Parameter module itself.
global using GetPricingTemplateQuery = Parameter.Contracts.PricingTemplates.GetPricingTemplateQuery;
global using GetPricingTemplateResult = Parameter.Contracts.PricingTemplates.GetPricingTemplateResult;
