using Auth.Application.Services;
using Auth.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Auth.Application.Features.Menu.UpdateMenuItem;

public class UpdateMenuItemCommandHandler(AuthDbContext dbContext, IMenuTreeCache cache)
    : ICommandHandler<UpdateMenuItemCommand, UpdateMenuItemResult>
{
    private const int MaxTreeDepth = 3;

    public async Task<UpdateMenuItemResult> Handle(UpdateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var item = await dbContext.MenuItems
                       .Include(m => m.Translations)
                       .FirstOrDefaultAsync(m => m.Id == command.Id, cancellationToken)
                   ?? throw new NotFoundException("MenuItem", command.Id);

        var iconStyle = Enum.Parse<IconStyle>(command.IconStyle, true);

        // Reparent: validate cycle + scope + depth before applying
        if (command.ParentId != item.ParentId)
        {
            if (command.ParentId is { } newParentId)
            {
                if (newParentId == item.Id)
                    throw new DomainException("A menu item cannot be its own parent.");

                var parent = await dbContext.MenuItems
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(m => m.Id == newParentId, cancellationToken)
                             ?? throw new NotFoundException("ParentMenuItem", newParentId);

                if (parent.Scope != item.Scope)
                    throw new DomainException("Parent menu item must belong to the same scope.");

                // Cycle detection: walk up from new parent; we must not encounter item.Id
                var visited = new HashSet<Guid>();
                Guid? cursor = newParentId;
                var ancestorDepth = 0;
                while (cursor is { } cid)
                {
                    if (cid == item.Id)
                        throw new DomainException("Reparenting would create a cycle.");
                    if (!visited.Add(cid))
                        throw new DomainException("Menu tree contains a cycle.");

                    var p = await dbContext.MenuItems
                        .AsNoTracking()
                        .Where(m => m.Id == cid)
                        .Select(m => new { m.ParentId })
                        .FirstOrDefaultAsync(cancellationToken);
                    if (p is null) break;
                    ancestorDepth++;
                    cursor = p.ParentId;
                    if (ancestorDepth > 20) throw new DomainException("Menu tree too deep.");
                }

                // Depth of new position
                var subtreeDepth = await GetSubtreeDepthAsync(item.Id, cancellationToken);
                var newDepth = (ancestorDepth + 1) + subtreeDepth; // parent chain + this node + deepest descendant
                if (newDepth > MaxTreeDepth)
                    throw new DomainException($"Menu tree depth cannot exceed {MaxTreeDepth} levels.");
            }

            item.Reparent(command.ParentId);
        }

        item.Update(
            command.Path,
            MenuIcon.Create(command.IconName, iconStyle),
            command.IconColor,
            command.SortOrder,
            command.ViewPermissionCode,
            command.EditPermissionCode);

        var newTranslations = command.Translations
            .Select(t => MenuItemTranslation.Create(t.LanguageCode, t.Label))
            .ToList();

        // Remove existing translations tracked by EF so composite PK doesn't conflict
        foreach (var existing in item.Translations.ToList())
            dbContext.Remove(existing);
        item.ReplaceTranslations(newTranslations);

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        return new UpdateMenuItemResult(true);
    }

    private async Task<int> GetSubtreeDepthAsync(Guid rootId, CancellationToken cancellationToken)
    {
        // Returns the max depth of descendants under rootId (1-based, so 1 means rootId has no children)
        var all = await dbContext.MenuItems
            .AsNoTracking()
            .Select(m => new { m.Id, m.ParentId })
            .ToListAsync(cancellationToken);

        var childLookup = all.GroupBy(m => m.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        int Dfs(Guid id)
        {
            if (!childLookup.TryGetValue(id, out var kids) || kids.Count == 0)
                return 1;
            return 1 + kids.Max(Dfs);
        }

        return Dfs(rootId);
    }
}
