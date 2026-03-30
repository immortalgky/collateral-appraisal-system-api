namespace Auth.Application.Features.Groups.GetGroups;

public record GetGroupsQuery(string? Search, string? Scope, int PageNumber = 1, int PageSize = 20)
    : IQuery<GetGroupsResult>;
