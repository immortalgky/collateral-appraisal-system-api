namespace Parameter.Parameters.Features;

public class CreateParametersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/parameter", async (
            CreateParameterCommand request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = request.Adapt<CreateParameterCommand>();


            var result = await sender.Send(command, cancellationToken);
            var response = result.Adapt<CreateParameterResponse>();

            return Results.Ok(response);
        })
        .WithName("CreateParameters")
        .Produces<List<ParameterDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create parameter")
        .WithDescription("Create new parameter")
        .WithTags("Parameter");
    }
}
