namespace Auth.Application.Features.Menu.Dtos;

public record MenuTreeNodeDto(
    Guid Id,
    string ItemKey,
    string? Path,
    string IconName,
    string IconStyle,
    string? IconColor,
    int SortOrder,
    Dictionary<string, string> Labels,
    bool CanEdit,
    List<MenuTreeNodeDto> Children);
