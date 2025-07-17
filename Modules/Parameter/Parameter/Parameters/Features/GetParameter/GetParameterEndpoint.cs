using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Parameter.Parameters.Features.GetParameter;

public class GetParameterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/parameter", async ([AsParameters]ParameterDto parameter, [FromServices]ISender sender) =>
        {
            var query = new GetParameterQuery(parameter);

            var result = await sender.Send(query);

            var response = result.Adapt<GetParameterResponse>();

            return Results.Ok(response.Parameter);
        });       
    }

}