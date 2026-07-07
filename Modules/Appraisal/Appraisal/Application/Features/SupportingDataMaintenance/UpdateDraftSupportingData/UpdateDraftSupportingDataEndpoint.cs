namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateDraftSupportingData;

public class UpdateDraftSupportingDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/supporting-data/draft/{supportingId:guid}", async (
            Guid supportingId,
            UpdateDraftSupportingDataRequest request,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var command = new UpdateDraftSupportingDataCommand(supportingId, new SupportingDataHeaderDto(
                request.Header.ImportChannel,
                request.Header.ImportDate,
                request.Header.SourceOfData,
                request.Header.Description,
                request.Header.Decision,
                request.Header.Remark,
                request.Header.AppraisalCompanyId)
                );

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<UpdateDraftSupportingDataResponse>();

            return Results.Ok(response);
        })
        .WithName("UpdateDraftSupportingData")
        .Produces<UpdateDraftSupportingDataResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update an existing draft supporting data")
        .WithDescription("Update an existing draft supporting data record for appraisal reference.")
        .WithTags("SupportingData")
        .RequireAuthorization();
    }
}