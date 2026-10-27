using System.Security.Claims;
using Auth.Infrastructure.Session;
using OpenIddict.Abstractions;

namespace Auth.Application.Features.Auth.Passkeys;

/// <summary>
/// The signed-in user's own passkeys: list, add (two steps, the browser's WebAuthn dialog in between), rename, remove.
/// Every wrong state answers 400 with a `detail` and an `errorCode`, never 401: the SPA signs the user out on any 401.
/// </summary>
public class PasskeyEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth/me/passkeys").RequireAuthorization().WithTags("Auth");

        group.MapGet("",
                async (ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new GetMyPasskeysQuery(UserId(user)), ct)))
            .WithName("GetMyPasskeys")
            .Produces<IReadOnlyList<PasskeyDto>>()
            .WithSummary("List my passkeys");

        group.MapPost("/register/options",
                async (ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new BeginPasskeyRegistrationCommand(UserId(user), SessionId(user)), ct)))
            .WithName("BeginPasskeyRegistration")
            .Produces<PasskeyRegistrationOptionsResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Start adding a passkey")
            .WithDescription(
                "Returns `options` (input for PublicKeyCredential.parseCreationOptionsFromJSON) and a sealed `state` to send back. "
                + "Needs a sign-in within the last 10 minutes (PasskeyReauthRequired) and fewer than 10 passkeys (PasskeyLimitReached).");

        group.MapPost("/register",
                async (RegisterPasskeyRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(
                        new RegisterPasskeyCommand(UserId(user), request.State, request.Name, request.Credential), ct)))
            .WithName("RegisterPasskey")
            .Produces<PasskeyDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Finish adding a passkey")
            .WithDescription("`credential` is the browser's credential.toJSON(). Errors: PasskeyStateExpired, PasskeyRegistrationFailed, "
                             + "PasskeyAlreadyRegistered, PasskeyLimitReached, PasskeyNameInvalid.");

        group.MapPut("/{id:guid}",
                async (Guid id, RenamePasskeyRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                {
                    await sender.Send(new RenamePasskeyCommand(UserId(user), id, request.Name), ct);
                    return Results.NoContent();
                })
            .WithName("RenamePasskey")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Rename one of my passkeys");

        group.MapDelete("/{id:guid}",
                async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                {
                    await sender.Send(new DeletePasskeyCommand(UserId(user), id), ct);
                    return Results.NoContent();
                })
            .WithName("DeletePasskey")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove one of my passkeys");
    }

    private static Guid UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException();

    private static Guid? SessionId(ClaimsPrincipal user) =>
        Guid.TryParseExact(user.FindFirst(AuthSchemes.SessionIdClaim)?.Value, "N", out var id) ? id : null;
}
