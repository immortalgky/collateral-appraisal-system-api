namespace Request.Requests.Features.CreateDraftRequest;

public class CreateDraftRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/draft",
                async (CreateDraftRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<CreateDraftRequestCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateDraftRequestResponse>();

                    return Results.Created($"/requests/{response.Id}", response);
                })
            .WithName("CreateDraftRequest")
            .Produces<CreateDraftRequestResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a draft request")
            .WithDescription(
                "Creates a draft request in the system. The request details are provided in the request body.")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}