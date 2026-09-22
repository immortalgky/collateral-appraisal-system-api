namespace Appraisal.Application.Features.FireInsuranceRates.GetFireInsuranceRates;

public class GetFireInsuranceRatesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/fire-insurance-rates",
                async (string? propertyKind, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new GetFireInsuranceRatesQuery(propertyKind), cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("GetFireInsuranceRates")
            .Produces<GetFireInsuranceRatesResult>(StatusCodes.Status200OK)
            .WithSummary("Get fire insurance coverage rates")
            .WithDescription(
                "Reference data: the coverage rate per sq.m. for each building condition. "
                + "Optional propertyKind filter ('Condo' or 'LandAndBuilding'); omit it for all kinds.")
            .WithTags("Appraisal");
    }
}
