namespace Appraisal.Application.Features.Quotations.ShortlistQuotation;

public class ShortlistQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/quotations/{companyQuotationId:guid}/shortlist",
                async (
                    Guid id,
                    Guid companyQuotationId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new ShortlistQuotationCommand(id, companyQuotationId), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("ShortlistQuotation")
            .Produces<ShortlistQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add company quotation to shortlist")
            .WithDescription("Admin adds a company's submitted quotation to the review shortlist.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
