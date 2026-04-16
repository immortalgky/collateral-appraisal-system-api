using Auth.Application.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Auth.Application.Features.Menu.DeleteMenuItem;

public class DeleteMenuItemCommandHandler(AuthDbContext dbContext, IMenuTreeCache cache)
    : ICommandHandler<DeleteMenuItemCommand, DeleteMenuItemResult>
{
    public async Task<DeleteMenuItemResult> Handle(DeleteMenuItemCommand command, CancellationToken cancellationToken)
    {
        // Translations are cascade-deleted via FK — no need to Include them here.
        var item = await dbContext.MenuItems
                       .FirstOrDefaultAsync(m => m.Id == command.Id, cancellationToken)
                   ?? throw new NotFoundException("MenuItem", command.Id);

        item.EnsureDeletable();

        var hasChildren = await dbContext.MenuItems.AnyAsync(m => m.ParentId == item.Id, cancellationToken);
        if (hasChildren)
            throw new DomainException("Cannot delete a menu item that has children. Delete or reparent its children first.");

        dbContext.MenuItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        return new DeleteMenuItemResult(true);
    }
}
