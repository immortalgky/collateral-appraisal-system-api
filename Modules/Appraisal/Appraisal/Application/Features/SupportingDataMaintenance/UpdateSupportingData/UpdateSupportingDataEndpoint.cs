namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateSupportingData;

public class UpdateSupportingDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/supporting-data/{supportingId:guid}", async (
            Guid supportingId,
            UpdateSupportingDataRequest request,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var command = new UpdateSupportingDataCommand(supportingId, new SupportingDataHeaderDto(request.Header.ImportChannel,
            request.Header.ImportDate,
            request.Header.SourceOfData,
            request.Header.AppraisalCompany,
            request.Header.Description,
            request.Header.Remark));

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<UpdateSupportingDataResponse>();

            return Results.Ok(response);
        })
        .WithName("UpdateSupportingData")
        .Produces<UpdateSupportingDataResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update an existing supporting data")
        .WithDescription("Update an existing supporting data record for appraisal reference.")
        .WithTags("SupportingData");
    }
}