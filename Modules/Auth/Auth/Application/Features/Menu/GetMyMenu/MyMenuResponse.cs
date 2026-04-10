using Auth.Application.Features.Menu.Dtos;

namespace Auth.Application.Features.Menu.GetMyMenu;

public record MyMenuResponse(
    List<MenuTreeNodeDto> Main,
    List<MenuTreeNodeDto> Appraisal);
