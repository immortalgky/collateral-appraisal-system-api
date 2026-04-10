using Auth.Application.Features.Menu.GetMenuItems;
using Auth.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Auth.Application.Features.Menu.GetMenuItemById;

public class GetMenuItemByIdQueryHandler(AuthDbContext dbContext)
    : IQueryHandler<GetMenuItemByIdQuery, GetMenuItemByIdResult>
{
    private static readonly IReadOnlyDictionary<Guid, List<MenuItem>> EmptyChildren =
        new Dictionary<Guid, List<MenuItem>>();

    public async Task<GetMenuItemByIdResult> Handle(GetMenuItemByIdQuery query, CancellationToken cancellationToken)
    {
        var item = await dbContext.MenuItems
                       .AsNoTracking()
                       .Include(m => m.Translations)
                       .FirstOrDefaultAsync(m => m.Id == query.Id, cancellationToken)
                   ?? throw new NotFoundException("MenuItem", query.Id);

        // Detail view doesn't need nested children — pass an empty lookup so the DTO
        // returns Children=[]. The admin form only needs the item's own fields.
        return new GetMenuItemByIdResult(GetMenuItemsQueryHandler.ToDto(item, EmptyChildren));
    }
}
