using Appraisal.Application.Features.Quotations.AutoAssign;

namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public class FinalizeQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/finalize",
                (
                    Guid id,
                    FinalizeQuotationRequest request,
                    ISender sender,
                    ILogger<FinalizeQuotationEndpoint> logger,
                    CancellationToken cancellationToken
                ) => FinalizeAsync(id, request, sender, logger, cancellationToken))
            .WithName("FinalizeQuotation")
            .Produces<FinalizeQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Finalize quotation")
            .WithDescription("Admin commits the final price and marks the quotation as Finalized. Triggers ext assignment creation and workflow task completion (Track 2).")
            .WithTags("Quotation")
            .RequireAuthorization();
    }

    internal static async Task<IResult> FinalizeAsync(
        Guid id,
        FinalizeQuotationRequest request,
        ISender sender,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var command = new FinalizeQuotationCommand(
            id,
            request.CompanyQuotationId,
            request.Reason,
            AutoAssignRequested: request.AutoAssign is not null,
            AutoAssignStaff: request.AutoAssign?.Staff);
        var result = await sender.Send(command, cancellationToken);

        if (request.AutoAssign is null)
            return Results.Ok(result);

        // Finalize has committed, so nothing below may turn this call into an HTTP error.
        // The handler records an outcome for every appraisal once the quotation is loaded; if it
        // throws before that, auto-assign did not run for any appraisal.
        IReadOnlyList<AutoAssignItemResult> autoAssignResults;
        try
        {
            autoAssignResults = await sender.Send(
                new AutoAssignQuotationAppraisalsCommand(id, request.AutoAssign.Staff ?? []),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto-assign failed after quotation {QuotationId} was finalized", id);
            autoAssignResults = result.AppraisalIds
                .Select(appraisalId => new AutoAssignItemResult(
                    appraisalId, AutoAssignOutcome.Failed, AutoAssignQuotationAppraisalsCommandHandler.UnexpectedStopMessage,
                    AutoAssignReason.UnexpectedStop))
                .ToList();
        }

        return Results.Ok(result with { AutoAssignResults = autoAssignResults });
    }
}
