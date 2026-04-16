using Auth.Application.Services;
using Auth.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Auth.Application.Features.Menu.CreateMenuItem;

public class CreateMenuItemCommandHandler(AuthDbContext dbContext, IMenuTreeCache cache)
    : ICommandHandler<CreateMenuItemCommand, CreateMenuItemResult>
{
    private const int MaxTreeDepth = 3;

    public async Task<CreateMenuItemResult> Handle(CreateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var scope = Enum.Parse<MenuScope>(command.Scope, true);
        var iconStyle = Enum.Parse<IconStyle>(command.IconStyle, true);

        if (await dbContext.MenuItems.AnyAsync(m => m.ItemKey == command.ItemKey, cancellationToken))
            throw new DomainException($"Menu item with ItemKey '{command.ItemKey}' already exists.");

        if (command.ParentId is { } parentId)
        {
            var parent = await dbContext.MenuItems
                             .AsNoTracking()
                             .FirstOrDefaultAsync(m => m.Id == parentId, cancellationToken)
                         ?? throw new NotFoundException("ParentMenuItem", parentId);

            if (parent.Scope != scope)
                throw new DomainException("Parent menu item must belong to the same scope.");

            // Depth check — walk up the parent chain
            var depth = await CalculateDepthAsync(parentId, cancellationToken) + 1; // +1 for new node
            if (depth >= MaxTreeDepth)
                throw new DomainException($"Menu tree depth cannot exceed {MaxTreeDepth} levels.");
        }

        var translations = command.Translations
            .Select(t => MenuItemTranslation.Create(t.LanguageCode, t.Label))
            .ToList();

        var item = MenuItem.Create(
            command.ItemKey,
            scope,
            command.ParentId,
            command.Path,
            MenuIcon.Create(command.IconName, iconStyle),
            command.IconColor,
            command.SortOrder,
            command.ViewPermissionCode,
            command.EditPermissionCode,
            translations,
            isSystem: false);

        dbContext.MenuItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        return new CreateMenuItemResult(item.Id);
    }

    private async Task<int> CalculateDepthAsync(Guid nodeId, CancellationToken cancellationToken)
    {
        var depth = 0;
        var currentId = (Guid?)nodeId;
        var visited = new HashSet<Guid>();

        while (currentId is { } id)
        {
            if (!visited.Add(id))
                throw new DomainException("Menu tree contains a cycle.");

            var parent = await dbContext.MenuItems
                .AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new { m.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (parent is null) break;
            depth++;
            currentId = parent.ParentId;
            if (depth > 20) throw new DomainException("Menu tree too deep.");
        }

        return depth;
    }
}
