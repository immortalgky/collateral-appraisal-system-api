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
    List<string> Roles);

public record GetUsersResult(IEnumerable<UserListItemDto> Items, long Count, int PageNumber, int PageSize);
