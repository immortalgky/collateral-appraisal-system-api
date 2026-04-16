namespace Auth.Application.Features.Menu.ReorderMenuItems;

public record ReorderMenuItemDto(Guid Id, Guid? ParentId, int SortOrder);

public record ReorderMenuItemsRequest(List<ReorderMenuItemDto> Items);
