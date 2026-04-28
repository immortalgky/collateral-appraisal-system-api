using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.SaveDraftQuotation;

public class SaveDraftQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/draft",
                async (
                    Guid id,
                    SaveDraftQuotationRequest request,
                    ISender sender,
                    ICurrentUserService currentUserService,
                    CancellationToken cancellationToken
                ) =>
                {
                    // CompanyId is derived from the JWT company_id claim — never from the URL.
                    var companyId = currentUserService.CompanyId
                        ?? throw new UnauthorizedAccessException(
                            "External company user has no company_id claim");

                    var items = request.Items.Select(i => new SaveDraftQuotationItem(
                        i.QuotationRequestItemId,
                        i.AppraisalId,
                        i.ItemNumber,
                        i.FeeAmount,
                        i.Discount,
                        i.NegotiatedDiscount,
                        i.VatPercent,
                        i.EstimatedDays,
                        i.ItemNotes)).ToList();

                    var command = new SaveDraftQuotationCommand(
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
            .WithName("SaveDraftQuotation")
            .Produces<SaveDraftQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save company quotation draft")
            .WithDescription(
                "Maker (ExtAdmin) or Checker (ExtAppraisalChecker) saves or updates a draft quotation for the RFQ. " +
                "The company is identified from the JWT company_id claim. " +
                "Idempotent: re-saving replaces all items on the existing draft.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
