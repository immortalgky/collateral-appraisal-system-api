namespace Auth.Domain.Auth.Features.UpdateProfile;

public record UpdateProfileResponse(
    Guid Id,
    string Username,
    string? Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? AvatarUrl,
    string? Position,
    string? Department
);
