namespace Appraisal.Application.Features.Quotations.GetQuotationActivityLog;

public class GetQuotationActivityLogEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/quotations/{id:guid}/activity-log",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetQuotationActivityLogQuery(id);
                    var rows = await sender.Send(query, cancellationToken);
                    return Results.Ok(rows);
                })
            .WithName("GetQuotationActivityLog")
            .Produces<List<QuotationActivityLogRow>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get quotation activity log")
            .WithDescription("Returns the append-only audit trail for a quotation request, ordered chronologically. " +
                             "Ext-company callers see only rows belonging to their own company.")
            .WithTags("Quotations")
            .RequireAuthorization();
    }
}
