namespace Auth.Application.Features.Menu.GetMyMenu;

public record GetMyMenuQuery(Guid UserId, string? ActivityId = null) : IQuery<MyMenuResponse>;
