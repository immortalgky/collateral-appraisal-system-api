namespace Appraisal.Application.Features.Appraisals.SaveCondoPMAPropertyDraft;

public class SaveCondoPMAPropertyDraftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/condo-pma/draft",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    SaveCondoPMAPropertyDraftRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveCondoPMAPropertyDraftCommand>()
                        with
                        {
                            AppraisalId = appraisalId, PropertyId = propertyId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("SaveCondoPMAPropertyDraft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save condo pma property draft")
            .WithDescription(
                "Persist a draft of the condo pma property detail (stamps ExternalSyncStatus=Pending) without triggering the LOS webhook.")
            .WithTags("Appraisal Properties");
    }
}
