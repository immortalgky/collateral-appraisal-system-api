using Auth.Application.Features.Menu.Dtos;

namespace Auth.Application.Features.Menu.CreateMenuItem;

public record CreateMenuItemRequest(
    string ItemKey,
    string Scope,
    Guid? ParentId,
    string? Path,
    string IconName,
    string IconStyle,
    string? IconColor,
    int SortOrder,
    string ViewPermissionCode,
    string? EditPermissionCode,
    List<MenuItemTranslationDto> Translations);
