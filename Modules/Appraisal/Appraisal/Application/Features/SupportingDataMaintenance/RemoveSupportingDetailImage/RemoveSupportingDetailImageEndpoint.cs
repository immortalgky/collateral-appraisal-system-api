using Carter;
using MediatR;

namespace Appraisal.Application.Features.SupportingDataMaintenance.RemoveSupportingDetailImage;

/// <summary>
/// Endpoint: DELETE /supporting-data/{supportingId}/details/{detailId}/images/{imageId}
/// Removes a photo from a supporting detail.
/// </summary>
public class RemoveSupportingDetailImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/supporting-data/{supportingId:guid}/details/{detailId:guid}/images/{imageId:guid}",
                async (
                    Guid supportingId,
                    Guid detailId,
                    Guid imageId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new RemoveSupportingDetailImageCommand(supportingId, detailId, imageId);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("RemoveSupportingDetailImage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove image from supporting detail")
            .WithDescription("Removes a photo from a supporting detail record.")
            .WithTags("SupportingData");
    }
}
