namespace Auth.Domain.Auth.Features.UpdateProfile;

public record UpdateProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department
) : ICommand<UpdateProfileResult>;
