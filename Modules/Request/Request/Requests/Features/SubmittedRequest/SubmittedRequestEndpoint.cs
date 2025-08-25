namespace Request.Requests.Features.SubmittedRequest;

public class SubmittedRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/request/{requestId:long}/submitted", async (long requestId, ISender sender) =>
        {
            var query = new SubmittedRequestQuery(requestId);
            
            var result = await sender.Send(query);

            var response = result.Adapt<SubmittedRequestResponse>();
            
            return Results.Ok(response);
        });
    }
}