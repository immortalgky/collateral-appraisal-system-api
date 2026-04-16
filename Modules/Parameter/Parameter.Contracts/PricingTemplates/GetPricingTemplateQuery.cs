using MediatR;

namespace Parameter.Contracts.PricingTemplates;

/// <summary>
/// Cross-module query: fetch the full blueprint for a pricing template by code.
/// Handler lives in Parameter module; Appraisal module dispatches via MediatR.
/// </summary>
public record GetPricingTemplateQuery(string Code) : IRequest<GetPricingTemplateResult>;
