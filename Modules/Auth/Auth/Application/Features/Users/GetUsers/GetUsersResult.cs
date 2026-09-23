namespace Auth.Application.Features.Users.GetUsers;

public record UserListItemDto(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string? AvatarUrl,
    string? Position,
    string? Department,
    Guid? CompanyId,
    string AuthSource,
    List<string> Roles,
    bool IsActive,
    bool IsLocked,
    // Ad-hoc account usable only inside an admin-opened window; AccessExpiresAt is when that window ends.
    bool IsTemporaryAccess,
    DateTime? AccessExpiresAt);

public record GetUsersResult(IEnumerable<UserListItemDto> Items, long Count, int PageNumber, int PageSize);
