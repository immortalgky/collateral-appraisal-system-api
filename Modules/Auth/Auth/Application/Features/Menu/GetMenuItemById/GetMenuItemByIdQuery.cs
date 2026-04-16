using Auth.Application.Features.Menu.Dtos;

namespace Auth.Application.Features.Menu.GetMenuItemById;

public record GetMenuItemByIdQuery(Guid Id) : IQuery<GetMenuItemByIdResult>;

public record GetMenuItemByIdResult(MenuItemAdminDto Item);
