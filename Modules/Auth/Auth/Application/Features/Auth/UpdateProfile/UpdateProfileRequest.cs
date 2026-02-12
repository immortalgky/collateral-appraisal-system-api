namespace Auth.Domain.Auth.Features.UpdateProfile;

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department
);
