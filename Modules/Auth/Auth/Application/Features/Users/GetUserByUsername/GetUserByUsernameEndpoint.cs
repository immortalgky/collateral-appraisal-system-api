namespace Auth.Application.Features.Users.GetUserByUsername;

public class GetUserByUsernameEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/users/by-username/{username}",
                async (string username, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new GetUserByUsernameQuery(username), cancellationToken)))
            .WithName("GetUserByUsername")
            .Produces<UserContactDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a user's contact card by exact username")
            .WithDescription("Contact details only (no roles or permissions), found by username (case-insensitive); 404 when none matches. Signed-in users only, by the default policy.")
            .WithTags("User");
    }
}
