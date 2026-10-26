namespace Auth.Domain.Auth.Features.UpdateProfile;

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? AvatarUrl,
    string? Position,
    string? Department
);
