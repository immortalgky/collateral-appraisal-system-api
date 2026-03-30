namespace Auth.Application.Features.Users.GetUsers;

public record GetUsersQuery(string? Search, string? Scope, int PageNumber = 1, int PageSize = 20)
    : IQuery<GetUsersResult>;
