namespace Auth.Application.Features.Groups.GetGroups;

public record GroupListItemDto(Guid Id, string Name, string Description, string Scope, Guid? CompanyId, int UserCount);

public record GetGroupsResult(IEnumerable<GroupListItemDto> Items, long Count, int PageNumber, int PageSize);
