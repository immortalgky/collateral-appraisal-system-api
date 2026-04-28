namespace Appraisal.Application.Features.Quotations.GetQuotations;

public class GetQuotationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/quotations",
                async (
                    [AsParameters] PaginationRequest request,
                    Guid? appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetQuotationsQuery(request, appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetQuotationsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetQuotations")
            .Produces<GetQuotationsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get all quotations")
            .WithDescription("Retrieve quotation requests with pagination. Results are scoped by caller role.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}