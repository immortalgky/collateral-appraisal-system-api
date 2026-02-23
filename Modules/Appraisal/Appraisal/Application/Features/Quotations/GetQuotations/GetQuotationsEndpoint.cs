namespace Appraisal.Application.Features.Quotations.GetQuotations;

public class GetQuotationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/quotations",
                async (
                    [AsParameters] PaginationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetQuotationsQuery(request);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetQuotationsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetQuotations")
            .Produces<GetQuotationsResponse>(StatusCodes.Status200OK)
            .WithSummary("Get all quotations")
            .WithDescription("Retrieve all quotation requests with pagination.")
            .WithTags("Quotation");
    }
}