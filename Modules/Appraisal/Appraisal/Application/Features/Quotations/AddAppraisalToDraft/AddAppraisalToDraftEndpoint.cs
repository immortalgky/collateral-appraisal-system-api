namespace Appraisal.Application.Features.Quotations.AddAppraisalToDraft;

public class AddAppraisalToDraftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/appraisals",
                async (
                    Guid id,
                    AddAppraisalToDraftRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddAppraisalToDraftCommand(
                        QuotationRequestId: id,
                        AppraisalId: request.AppraisalId,
                        AppraisalNumber: request.AppraisalNumber,
                        PropertyType: request.PropertyType,
                        PropertyLocation: request.PropertyLocation,
                        EstimatedValue: request.EstimatedValue,
                        MaxAppraisalDays: request.MaxAppraisalDays);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("AddAppraisalToDraft")
            .Produces<AddAppraisalToDraftResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Add an appraisal to a Draft quotation")
            .WithDescription("Adds an appraisal to an existing Draft RFQ. Allowed only while Status=Draft and caller owns the draft. " +
                             "Appraisal must not be in another non-terminal quotation.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
