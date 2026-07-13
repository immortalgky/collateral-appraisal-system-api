namespace Appraisal.Application.Features.Appraisals.SaveLandPMAPropertyDraft;

public class SaveLandPMAPropertyDraftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/land-building-pma/draft",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    SaveLandPMAPropertyDraftRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveLandPMAPropertyDraftCommand>()
                        with
                        {
                            AppraisalId = appraisalId, PropertyId = propertyId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("SaveLandPMAPropertyDraft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save land pma property draft")
            .WithDescription(
                "Persist a draft of the land pma property detail (stamps ExternalSyncStatus=Pending) without triggering the LOS webhook.")
            .WithTags("Appraisal Properties");
    }
}
