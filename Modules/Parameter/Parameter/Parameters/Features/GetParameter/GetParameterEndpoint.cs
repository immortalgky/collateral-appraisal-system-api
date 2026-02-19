namespace Parameter.Parameters.Features.GetParameter;

public class GetParametersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/parameters", async ([AsParameters] ParameterDto parameter, ISender sender) =>
        {
            var query = new GetParametersQuery(parameter);

            var result = await sender.Send(query);

            var response = result.Adapt<GetParametersResponse>();

            return Results.Ok(response.Parameters);
        })
        .WithName("GetParameters")
        .Produces<List<ParameterDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get parameters")
        .WithDescription("Retrieve a list of parameters filtered by group, country, language, code, or active status.")
        .WithTags("Parameter");
    }
}
