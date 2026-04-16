namespace Auth.Application.Features.Menu.GetMyMenu;

public record GetMyMenuQuery(Guid UserId) : IQuery<MyMenuResponse>;
