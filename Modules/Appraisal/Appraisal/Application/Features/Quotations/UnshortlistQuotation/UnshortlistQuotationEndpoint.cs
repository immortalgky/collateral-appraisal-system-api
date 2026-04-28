namespace Appraisal.Application.Features.Quotations.UnshortlistQuotation;

public class UnshortlistQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/quotations/{id:guid}/quotations/{companyQuotationId:guid}/shortlist",
                async (
                    Guid id,
                    Guid companyQuotationId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new UnshortlistQuotationCommand(id, companyQuotationId), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("UnshortlistQuotation")
            .Produces<UnshortlistQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove company quotation from shortlist")
            .WithDescription("Admin removes a company's submitted quotation from the review shortlist.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
