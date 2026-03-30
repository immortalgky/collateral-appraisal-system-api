namespace Auth.Application.Features.Users.ResetPassword;

public class ResetPasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/users/{id:guid}/reset-password",
                async (Guid id, ResetPasswordRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new ResetPasswordCommand(id, request.NewPassword, request.ConfirmPassword);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("ResetPassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reset password (admin)")
            .WithDescription("Reset password for a local auth user. Requires USER_RESET_PASSWORD permission. No current password needed.")
            .WithTags("User")
            .RequireAuthorization("CanResetUserPassword");
    }
}
