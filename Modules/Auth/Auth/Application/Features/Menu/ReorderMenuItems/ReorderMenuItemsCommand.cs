namespace Auth.Application.Features.Menu.ReorderMenuItems;

public record ReorderMenuItemsCommand(List<ReorderMenuItemDto> Items) : ICommand<ReorderMenuItemsResult>;

public record ReorderMenuItemsResult(bool Success);

public record ReorderMenuItemsResponse(bool Success);
