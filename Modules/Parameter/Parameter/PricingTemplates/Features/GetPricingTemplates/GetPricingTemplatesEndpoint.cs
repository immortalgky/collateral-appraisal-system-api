namespace Parameter.PricingTemplates.Features.GetPricingTemplates;

public class GetPricingTemplatesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/pricing-templates",
                async (bool? activeOnly, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetPricingTemplatesQuery(activeOnly ?? false);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Templates);
                })
            .WithName("GetPricingTemplates")
            .Produces<List<PricingTemplateListDto>>(StatusCodes.Status200OK)
            .WithSummary("Get pricing templates")
            .WithDescription("Retrieve a list of income-approach pricing templates (DCF and Direct).")
            .WithTags("PricingTemplate");
    }
}
