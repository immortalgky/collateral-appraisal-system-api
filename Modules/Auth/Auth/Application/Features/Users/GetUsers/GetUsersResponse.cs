namespace Auth.Application.Features.Users.GetUsers;

public record GetUsersResponse(IEnumerable<UserListItemDto> Items, long Count, int PageNumber, int PageSize);
