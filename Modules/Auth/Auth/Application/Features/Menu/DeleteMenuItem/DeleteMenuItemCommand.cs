namespace Auth.Application.Features.Menu.DeleteMenuItem;

public record DeleteMenuItemCommand(Guid Id) : ICommand<DeleteMenuItemResult>;

public record DeleteMenuItemResult(bool Success);

public record DeleteMenuItemResponse(bool Success);
