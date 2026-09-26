namespace Auth.Domain.Auth.Features.RegisterUser;

public record RegisterUserCommand(
    string Username,
    string Password,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department,
    Guid? CompanyId,
    List<RegisterUserPermissionDto> Permissions,
    List<Guid> Roles
) : ICommand<RegisterUserResult>
{
    /// <summary>Leaves the password out entirely — see ChangePasswordCommand.ToString.</summary>
    public override string ToString() => $"{nameof(RegisterUserCommand)} {{ Username = {Username} }}";
}