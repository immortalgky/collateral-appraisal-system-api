namespace Auth.Application.Features.Companies.UpdateCompany;

public class UpdateCompanyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/companies/{id:guid}", async (Guid id, UpdateCompanyRequest request, ISender sender) =>
            {
                var command = new UpdateCompanyCommand(
                    id, request.Name, request.TaxId, request.Phone, request.Email,
                    request.Street, request.City, request.Province, request.PostalCode, request.IsActive);
                var result = await sender.Send(command);
                var response = result.Adapt<UpdateCompanyResponse>();
                return Results.Ok(response);
            })
            .WithName("UpdateCompany")
            .Produces<UpdateCompanyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update Company")
            .WithDescription("Update an existing company");
    }
}
