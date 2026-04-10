namespace Auth.Application.Features.Menu.Dtos;

public record MenuItemAdminDto(
    Guid Id,
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
    bool IsSystem,
    Dictionary<string, string> Labels,
    bool CanEdit,
    List<MenuItemAdminDto> Children,
    List<MenuItemTranslationDto> Translations);
