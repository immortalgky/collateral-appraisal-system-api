namespace Auth.Application.Features.Users.UpdateUserGroups;

public record UpdateUserGroupsRequest(List<Guid> GroupIds);
