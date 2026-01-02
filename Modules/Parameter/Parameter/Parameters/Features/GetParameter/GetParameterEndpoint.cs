namespace Parameter.Parameters.Features.GetParameter;

public class GetParameterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/parameter", async ([AsParameters]ParameterDto parameter, ISender sender) =>
        {
            var query = parameter.Adapt<GetParameterQuery>() with {Parameter = parameter};

            var result = await sender.Send(query);

            var response = result.Adapt<GetParameterResponse>();

            return Results.Ok(response.Parameter);
        });       
    }

}