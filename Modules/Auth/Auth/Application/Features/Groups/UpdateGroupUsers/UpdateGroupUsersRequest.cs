namespace Auth.Application.Features.Groups.UpdateGroupUsers;

public record UpdateGroupUsersRequest(List<Guid> UserIds);
