namespace Appraisal.Application.Features.Quotations.GetQuotationById;

public class GetQuotationByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/quotations/{id:guid}",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetQuotationByIdQuery(id);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetQuotationByIdResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetQuotationById")
            .Produces<GetQuotationByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get quotation by ID")
            .WithDescription("Retrieve a specific quotation request by its ID.")
            .WithTags("Quotation");
    }
}