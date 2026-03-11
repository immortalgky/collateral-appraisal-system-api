namespace Parameter.Addresses.Features.GetTitleAddresses;

public class GetTitleAddressesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/parameters/addresses/title", async (ISender sender) =>
        {
            var result = await sender.Send(new GetTitleAddressesQuery());

            return Results.Ok(result.Addresses);
        })
        .WithName("GetTitleAddresses")
        .Produces<List<AddressDto>>(StatusCodes.Status200OK)
        .WithSummary("Get title addresses")
        .WithDescription("Retrieve all Thai address entries from the Title (Department of Lands) dataset.")
        .WithTags("Parameter");
    }
}
