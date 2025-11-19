using Request.Services;

namespace Request.Requests.Features.UpdateDraftRequest;

public class UpdateDraftRequestEndpoint(IRequestService requestService) : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{id}/draft",
                async (Guid id, UpdateDraftRequestRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<RequestDto>();
                    command.Id = id;
                    var result = await requestService.UpdateRequestDraftAsync(command, sender, cancellationToken);

                    var response = result.Adapt<UpdateDraftRequestResponse>();

                    return Results.Ok(response);
                })
            .WithName("UpdateDraftRequest")
            .Produces<UpdateDraftRequestResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update an existing request")
            .WithDescription(
                "Updates an existing request in the system. The request details are provided in the request body.")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}
