using System;

namespace Request.Requests.Features.UpdateDraftRequest;

public class UpdateDraftRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{id}/draft",
                async (Guid id, UpdateDraftRequestRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<UpdateDraftRequestCommand>() with { Id = id };

                    var result = await sender.Send(command, cancellationToken);

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
