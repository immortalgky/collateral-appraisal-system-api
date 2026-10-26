namespace Document.Domain.UploadSessions.Features.CreateUploadSession;

public class CreateUploadSessionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/documents/session", async (
            HttpRequest httpRequest,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new CreateUploadSessionCommand(
                    httpRequest.Headers.UserAgent.ToString(),
                    httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);

            return Results.Ok(new CreateUploadSessionResponse(result.Id, result.ExpiresAt));
        })
        .WithName("CreateUploadSession")
        .WithTags("Documents")
        .Produces<CreateUploadSessionResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
        // .RequireAuthorization("CanUploadDocument");
    }
}