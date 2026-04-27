namespace Appraisal.Application.Features.Quotations.OpenNegotiation;

public class OpenNegotiationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/negotiations/open",
                async (
                    Guid id,
                    OpenNegotiationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new OpenNegotiationCommand(
                        id,
                        request.CompanyQuotationId,
                        request.Message);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("OpenNegotiation")
            .Produces<OpenNegotiationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Open a negotiation round")
            .WithDescription("Admin opens a negotiation round with the tentative winner by sending a note. The company sets the revised price by adjusting per-item discount when responding. Maximum 3 rounds per quotation.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
