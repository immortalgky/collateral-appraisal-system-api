using System;

namespace Request.Requests.Features.SubmitRequest;

public class SubmitRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{id}/submit",
                async (Guid id, SubmitRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<SubmitRequestCommand>() with { Id = id };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SubmitRequestResponse>();

                    return Results.Ok(response);
                })
            .WithName("SubmitRequest")
            .Produces<SubmitRequestResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Submit an existing request")
            .WithDescription(
                "Submit an existing request in the system. The request details are provided in the request body.")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}
