using Parameter.Contracts.PricingTemplates;

namespace Parameter.PricingTemplates.Features.GetPricingTemplate;

public class GetPricingTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/pricing-templates/{code}",
                async (string code, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetPricingTemplateQuery(code);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Template);
                })
            .WithName("GetPricingTemplate")
            .Produces<PricingTemplateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get pricing template by code")
            .WithDescription("Retrieve the full blueprint for an income-approach pricing template including sections, categories, and assumptions.")
            .WithTags("PricingTemplate");
    }
}
