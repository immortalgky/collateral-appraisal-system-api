using Auth.Application.Features.Menu.Dtos;

namespace Auth.Application.Features.Menu.GetMenuItems;

public record GetMenuItemsQuery(string? Scope) : IQuery<GetMenuItemsResult>;

public record GetMenuItemsResult(List<MenuItemAdminDto> Items);
