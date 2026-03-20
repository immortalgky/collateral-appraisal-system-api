namespace Auth.Application.Features.Companies.GetCompanyById;

public class GetCompanyByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/companies/{id:guid}", async (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new GetCompanyByIdQuery(id));
                var response = result.Adapt<GetCompanyByIdResponse>();
                return Results.Ok(response);
            })
            .WithName("GetCompanyById")
            .Produces<GetCompanyByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get Company By Id")
            .WithDescription("Get a company by its unique identifier");
    }
}
