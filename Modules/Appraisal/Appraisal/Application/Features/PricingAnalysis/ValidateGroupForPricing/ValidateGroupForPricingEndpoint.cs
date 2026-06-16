using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.ValidateGroupForPricing;

/// <summary>
/// Pre-flight validation for opening Pricing Analysis on a property group.
/// Read-only: returns a per-rule checklist the UI renders before navigating.
/// </summary>
public class ValidateGroupForPricingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/property-groups/{groupId:guid}/pricing-analysis/validation",
                async (
                    Guid groupId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new ValidateGroupForPricingQuery(groupId), cancellationToken);

                    var response = new ValidateGroupForPricingResponse(
                        result.Valid,
                        result.Steps
                            .Select(s => new PricingValidationStepResponse(
                                s.Key,
                                s.DisplayName,
                                s.Status.ToString(),
                                s.Messages))
                            .ToList());

                    return Results.Ok(response);
                }
            )
            .WithName("ValidateGroupForPricing")
            .Produces<ValidateGroupForPricingResponse>(StatusCodes.Status200OK)
            .WithSummary("Validate a property group is ready for pricing analysis")
            .WithDescription(
                "Runs maker-survey, building-detail, rental-schedule and mandatory-field checks " +
                "for the group and returns a per-rule pass/fail/skip checklist.")
            .WithTags("PricingAnalysis");
    }
}
