using Auth.Application.Features.Menu.Dtos;

namespace Auth.Application.Features.Menu.UpdateMenuItem;

public record UpdateMenuItemRequest(
    Guid? ParentId,
    string? Path,
    string IconName,
    string IconStyle,
    string? IconColor,
    int SortOrder,
    string ViewPermissionCode,
    string? EditPermissionCode,
    List<MenuItemTranslationDto> Translations);
