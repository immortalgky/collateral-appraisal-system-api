namespace Appraisal.Application.Features.Quotations.EditDraftQuotation;

public class EditDraftQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/quotations/{id:guid}/draft",
                async (
                    Guid id,
                    EditDraftQuotationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new EditDraftQuotationCommand(
                        QuotationRequestId: id,
                        DueDate: request.DueDate,
                        CompanyIds: request.CompanyIds);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("EditDraftQuotation")
            .Produces<EditDraftQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Edit a Draft quotation")
            .WithDescription("Updates the DueDate and the set of invited companies on a Draft RFQ. " +
                             "Allowed only while Status=Draft and caller owns the draft. " +
                             "Company list is replaced: companies not in the new list are removed, new ones are added.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
