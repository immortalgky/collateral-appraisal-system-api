using Auth.Application.Services;
using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Auth.Application.Features.Auth.TwoFactor;

/// <summary>
/// The signed-in user's own authenticator-app sign-in: set it up, turn it on or off, renew the recovery codes.
/// Every wrong code or wrong state answers 400 with a `detail`, never 401.
/// </summary>
public class TwoFactorEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth/me/two-factor").RequireAuthorization().WithTags("Auth");

        group.MapPost("/setup",
                async (ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new BeginTwoFactorSetupCommand(UserId(user)), ct)))
            .WithName("BeginTwoFactorSetup")
            .Produces<TwoFactorSetupInfo>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Start setting up an authenticator app")
            .WithDescription("Returns the (unverified) key with its QR code, creating the key on first use. Two-factor stays off until /enable.");

        group.MapPost("/enable",
                async (TwoFactorCodeRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new EnableTwoFactorCommand(UserId(user), request.Code), ct)))
            .WithName("EnableTwoFactor")
            .Produces<RecoveryCodesResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Turn two-factor on")
            .WithDescription("Verifies a code from the app and returns the 10 recovery codes, once.");

        group.MapPost("/disable",
                async (TwoFactorCodeRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                {
                    await sender.Send(new DisableTwoFactorCommand(UserId(user), request.Code), ct);
                    return Results.NoContent();
                })
            .WithName("DisableTwoFactor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Turn two-factor off")
            .WithDescription("Needs a current code. Refused when the password policy requires two-factor for this user.");

        group.MapPost("/recovery-codes",
                async (TwoFactorCodeRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new RegenerateRecoveryCodesCommand(UserId(user), request.Code), ct)))
            .WithName("RegenerateRecoveryCodes")
            .Produces<RecoveryCodesResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Replace the recovery codes")
            .WithDescription("Needs a current code. The old codes stop working.");

        group.MapPost("/new-device",
                async (TwoFactorCodeRequest request, ClaimsPrincipal user, ISender sender, IUserSessionService sessions,
                    CancellationToken ct) =>
                {
                    var result = await sender.Send(new NewTwoFactorDeviceCommand(UserId(user), request.Code), ct);

                    // Dropped only now that the reset's transaction has committed (see ResetTwoFactorEndpoint).
                    sessions.Evict(result.EndedSessionIds);
                    return Results.NoContent();
                })
            .WithName("NewTwoFactorDevice")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Set up two-factor on a new device")
            .WithDescription("Needs a current authenticator code or an unused recovery code. Resets two-factor like an admin reset (key and recovery codes removed, every session ended, this one included) and is not refused by the policy: a user it requires to have two-factor sets it up again at the next sign-in.");
    }

    private static Guid UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException();
}
