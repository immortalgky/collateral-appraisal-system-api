using Auth.Application.Services;
using Auth.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Auth.Application.Features.Menu.ReorderMenuItems;

public class ReorderMenuItemsCommandHandler(AuthDbContext dbContext, IMenuTreeCache cache)
    : ICommandHandler<ReorderMenuItemsCommand, ReorderMenuItemsResult>
{
    private const int MaxTreeDepth = 3;

    public async Task<ReorderMenuItemsResult> Handle(ReorderMenuItemsCommand command, CancellationToken cancellationToken)
    {
        var ids = command.Items.Select(i => i.Id).ToHashSet();
        var items = await dbContext.MenuItems
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken);

        if (items.Count != ids.Count)
            throw new NotFoundException("One or more menu items were not found.");

        // Apply updates in memory first
        foreach (var dto in command.Items)
        {
            var entity = items.First(m => m.Id == dto.Id);
            entity.Reparent(dto.ParentId);
            entity.Update(
                entity.Path,
                entity.Icon,
                entity.IconColor,
                dto.SortOrder,
                entity.ViewPermissionCode,
                entity.EditPermissionCode);
        }

        // Validate no cycles + depth + same-scope-parent after reorder
        var allForValidation = await dbContext.MenuItems
            .AsNoTracking()
            .Select(m => new { m.Id, m.ParentId, m.Scope })
            .ToListAsync(cancellationToken);

        // Build override map from tracked items (they reflect new state)
        var overrideMap = items.ToDictionary(i => i.Id, i => (ParentId: i.ParentId, Scope: i.Scope));
        var effectiveParent = allForValidation.ToDictionary(
            a => a.Id,
            a => overrideMap.TryGetValue(a.Id, out var o) ? (o.ParentId, o.Scope) : (a.ParentId, a.Scope));

        foreach (var entry in effectiveParent)
        {
            var (nodeId, (_, nodeScope)) = (entry.Key, entry.Value);
            var visited = new HashSet<Guid> { nodeId };
            var cursor = entry.Value.ParentId;
            var depth = 1;
            while (cursor is { } cid)
            {
                if (!visited.Add(cid))
                    throw new DomainException("Reorder would create a cycle.");

                if (!effectiveParent.TryGetValue(cid, out var parentEntry))
                    break;

                if (parentEntry.Scope != nodeScope)
                    throw new DomainException("Parent menu item must belong to the same scope.");

                depth++;
                if (depth > MaxTreeDepth)
                    throw new DomainException($"Menu tree depth cannot exceed {MaxTreeDepth} levels.");
                cursor = parentEntry.ParentId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        return new ReorderMenuItemsResult(true);
    }
}
