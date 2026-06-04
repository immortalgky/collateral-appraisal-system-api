namespace Auth.Application.Features.Users.GetUsers;

public record GetUsersQuery(
    string? Search,
    string? Scope,
    string? Role,
    bool? IsActive,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<GetUsersResult>;
