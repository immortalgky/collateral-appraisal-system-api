namespace Parameter.Parameters.Features.UpdateParameter;

public class UpdateParameterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/parameter/{parId:long}", async(
            long ParId,
            UpdateParameterRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
        var command = new UpdateParameterCommand(
            ParId: ParId,
            Code: request.Code,
            Description: request.Description,
            Country: request.Country,
            Language: request.Language,
            IsActive: request.IsActive,
            SeqNo: request.SeqNo
        );

            var result = await sender.Send(command, cancellationToken);

            return Results.Ok(new UpdateParameterResponse(result.Success));
        })
        .WithName("UpdateParameter")
        .Produces<List<ParameterDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Update parameter")
        .WithDescription("Update parameter detail country, language, code, or active status.")
        .WithTags("Parameter");
    }
}
