namespace Parameter.PricingParameters.Features.GetPricingParameters;

public class GetPricingParametersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/pricing-parameters",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetPricingParametersQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetPricingParameters")
            .Produces<GetPricingParametersResult>(StatusCodes.Status200OK)
            .WithSummary("Get pricing parameters")
            .WithDescription("Retrieve all reference lookup data for the income-approach pricing analysis: room types, job positions, tax brackets, assumption types, and assumption-to-method matrix.")
            .WithTags("PricingTemplate");
    }
}
