namespace Auth.Domain.Auth.Features.UpdateProfile;

public record UpdateProfileResult(
    Guid Id,
    string Username,
    string? Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department
);
