// All types have moved to Parameter.Contracts.PricingTemplates.
// Re-export via global aliases so existing usages within the Parameter module
// continue to compile without changes.
global using PricingTemplateDto = Parameter.Contracts.PricingTemplates.PricingTemplateDto;
global using PricingTemplateSectionDto = Parameter.Contracts.PricingTemplates.PricingTemplateSectionDto;
global using PricingTemplateCategoryDto = Parameter.Contracts.PricingTemplates.PricingTemplateCategoryDto;
global using PricingTemplateAssumptionDto = Parameter.Contracts.PricingTemplates.PricingTemplateAssumptionDto;
