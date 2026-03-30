namespace Auth.Application.Features.Groups.GetGroups;

public record GetGroupsResponse(IEnumerable<GroupListItemDto> Items, long Count, int PageNumber, int PageSize);
