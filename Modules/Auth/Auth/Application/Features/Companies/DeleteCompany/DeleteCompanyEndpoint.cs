namespace Auth.Application.Features.Companies.DeleteCompany;

public class DeleteCompanyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/companies/{id:guid}", async (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new DeleteCompanyCommand(id));
                var response = result.Adapt<DeleteCompanyResponse>();
                return Results.Ok(response);
            })
            .WithName("DeleteCompany")
            .Produces<DeleteCompanyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete Company")
            .WithDescription("Soft delete a company");
    }
}
