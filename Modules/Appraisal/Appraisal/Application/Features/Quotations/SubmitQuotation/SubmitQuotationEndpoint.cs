using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.SubmitQuotation;

public class SubmitQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/quotations/{id:guid}/submit",
                async (
                    Guid id,
                    SubmitQuotationRequest request,
                    ISender sender,
                    ICurrentUserService currentUserService,
                    CancellationToken cancellationToken
                ) =>
                {
                    // Derive companyId from the JWT company_id claim — never from the URL segment.
                    // EnsureCanSubmitQuotation will verify the claim matches the invitation.
                    var companyId = currentUserService.CompanyId
                        ?? throw new UnauthorizedAccessException(
                            "External company user has no company_id claim");

                    var items = request.Items.Select(i => new SubmitQuotationItemRequest(
                        i.QuotationRequestItemId,
                        i.AppraisalId,
                        i.ItemNumber,
                        i.QuotedPrice,
                        i.EstimatedDays,
                        FeeAmount: i.FeeAmount,
                        Discount: i.Discount,
                        NegotiatedDiscount: i.NegotiatedDiscount,
                        VatPercent: i.VatPercent,
                        ItemNotes: i.ItemNotes)).ToList();

                    var command = new SubmitQuotationCommand(
                        QuotationRequestId: id,
                        CompanyId: companyId,
                        QuotationNumber: request.QuotationNumber,
                        EstimatedDays: request.EstimatedDays,
                        Items: items,
                        ValidUntil: request.ValidUntil,
                        ProposedStartDate: request.ProposedStartDate,
                        ProposedCompletionDate: request.ProposedCompletionDate,
                        Remarks: request.Remarks,
                        TermsAndConditions: request.TermsAndConditions,
                        ContactName: request.ContactName,
                        ContactEmail: request.ContactEmail,
                        ContactPhone: request.ContactPhone);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("SubmitQuotation")
            .Produces<SubmitQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Submit company quotation")
            .WithDescription(
                "External company submits their bid for the RFQ. The company is identified from the JWT company_id claim. " +
                "Only the invited company may submit. One submission per company per RFQ.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
