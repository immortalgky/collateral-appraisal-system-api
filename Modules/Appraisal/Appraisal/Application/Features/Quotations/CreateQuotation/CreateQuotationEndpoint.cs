namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public class CreateQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations",
                async (
                    CreateQuotationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateQuotationCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateQuotationResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateQuotation")
            .Produces<CreateQuotationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create new quotation request")
            .WithDescription("Create a new Request for Quotation (RFQ) for external appraisal assignments.")
            .WithTags("Quotation");
    }
}