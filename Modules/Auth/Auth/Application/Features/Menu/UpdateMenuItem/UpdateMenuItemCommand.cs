using Auth.Application.Features.Menu.Dtos;

namespace Auth.Application.Features.Menu.UpdateMenuItem;

public record UpdateMenuItemCommand(
    Guid Id,
    Guid? ParentId,
    string? Path,
    string IconName,
    string IconStyle,
    string? IconColor,
    int SortOrder,
    string ViewPermissionCode,
    string? EditPermissionCode,
    List<MenuItemTranslationDto> Translations) : ICommand<UpdateMenuItemResult>;

public record UpdateMenuItemResult(bool Success);

public record UpdateMenuItemResponse(bool Success);
