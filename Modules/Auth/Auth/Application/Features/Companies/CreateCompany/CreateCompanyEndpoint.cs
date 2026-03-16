namespace Auth.Application.Features.Companies.CreateCompany;

public class CreateCompanyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/companies", async (CreateCompanyRequest request, ISender sender) =>
            {
                var command = request.Adapt<CreateCompanyCommand>();
                var result = await sender.Send(command);
                var response = result.Adapt<CreateCompanyResponse>();
                return Results.Created($"/companies/{response.Id}", response);
            })
            .WithName("CreateCompany")
            .Produces<CreateCompanyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create Company")
            .WithDescription("Create a new company");
    }
}
