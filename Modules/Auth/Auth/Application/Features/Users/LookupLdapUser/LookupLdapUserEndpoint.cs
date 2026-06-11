namespace Auth.Application.Features.Users.LookupLdapUser;

public class LookupLdapUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/users/ldap-lookup",
                async (string username, ISender sender, CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(username))
                        return Results.BadRequest("username is required.");

                    // Always 200: a not-found user is a normal result (Found=false), not an HTTP error,
                    // so the client can handle it inline without tripping global error handling.
                    var result = await sender.Send(new LookupLdapUserQuery(username), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("LookupLdapUser")
            .Produces<LookupLdapUserResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Look up a user in LDAP/AD (admin)")
            .WithDescription("Retrieve directory attributes for a username to pre-fill the user-creation screen. Does not validate a password.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}
