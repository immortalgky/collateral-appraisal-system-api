using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.SupportingDataMaintenance.AddSupportingDetailImage;

/// <summary>
/// Endpoint: POST /supporting-data/{supportingId}/details/{detailId}/images
/// Adds a photo to a supporting detail by linking an uploaded document.
/// </summary>
public class AddSupportingDetailImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/supporting-data/{supportingId:guid}/details/{detailId:guid}/images",
                async (
                    Guid supportingId,
                    Guid detailId,
                    AddSupportingDetailImageRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new AddSupportingDetailImageCommand(
                        supportingId,
                        detailId,
                        request.DocumentId,
                        request.StorageUrl,
                        request.FileName,
                        request.Title,
                        request.Description);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddSupportingDetailImageResponse>();

                    return Results.Ok(response);
                })
            .WithName("AddSupportingDetailImage")
            .Produces<AddSupportingDetailImageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add image to supporting detail")
            .WithDescription("Links an uploaded document as a photo on a supporting detail record.")
            .WithTags("SupportingData");
    }
}
