namespace Parameter.Addresses.Features.GetDopaAddresses;

public class GetDopaAddressesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/parameters/addresses/dopa", async (ISender sender) =>
        {
            var result = await sender.Send(new GetDopaAddressesQuery());

            return Results.Ok(result.Addresses);
        })
        .WithName("GetDopaAddresses")
        .Produces<List<AddressDto>>(StatusCodes.Status200OK)
        .WithSummary("Get DOPA addresses")
        .WithDescription("Retrieve all Thai address entries from the DOPA (Department of Provincial Administration) dataset.")
        .WithTags("Parameter");
    }
}
