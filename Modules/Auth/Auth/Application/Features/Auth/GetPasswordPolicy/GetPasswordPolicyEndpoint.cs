namespace Auth.Application.Features.Auth.GetPasswordPolicy;

public class GetPasswordPolicyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/password-policy",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetPasswordPolicyQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .RequireAuthorization()
            .WithName("GetPasswordPolicy")
            .Produces<GetPasswordPolicyResult>()
            .WithSummary("Get password policy")
            .WithDescription("Returns the configured password complexity requirements.")
            .WithTags("Auth");
    }
}
