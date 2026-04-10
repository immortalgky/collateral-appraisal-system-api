using Auth.Application.Features.Menu.Dtos;
using Auth.Domain.Menu;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Features.Menu.GetMenuItems;

public class GetMenuItemsQueryHandler(AuthDbContext dbContext)
    : IQueryHandler<GetMenuItemsQuery, GetMenuItemsResult>
{
    public async Task<GetMenuItemsResult> Handle(GetMenuItemsQuery query, CancellationToken cancellationToken)
    {
        var queryable = dbContext.MenuItems
            .AsNoTracking()
            .Include(m => m.Translations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Scope)
            && Enum.TryParse<MenuScope>(query.Scope, true, out var scope))
        {
            queryable = queryable.Where(m => m.Scope == scope);
        }

        var items = await queryable
            .OrderBy(m => m.Scope)
            .ThenBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

        // Build a parent → children lookup so we can return a nested tree of roots only.
        var childrenByParent = items
            .Where(m => m.ParentId.HasValue)
            .GroupBy(m => m.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.SortOrder).ToList());

        var roots = items
            .Where(m => !m.ParentId.HasValue)
            .OrderBy(m => m.SortOrder)
            .Select(m => ToDto(m, childrenByParent))
            .ToList();

        return new GetMenuItemsResult(roots);
    }

    /// <summary>
    /// Builds the admin DTO with nested children populated from a parent→children lookup.
    /// Used by both list (this handler) and detail (GetMenuItemByIdQueryHandler) — the
    /// detail variant passes an empty dictionary so children come back as an empty list.
    /// </summary>
    internal static MenuItemAdminDto ToDto(MenuItem m, IReadOnlyDictionary<Guid, List<MenuItem>> childrenByParent) =>
        new(
            m.Id,
            m.ItemKey,
            m.Scope.ToString(),
            m.ParentId,
            m.Path,
            m.Icon.Name,
            m.Icon.Style.ToString().ToLowerInvariant(),
            m.IconColor,
            m.SortOrder,
            m.ViewPermissionCode,
            m.EditPermissionCode,
            m.IsSystem,
            BuildLabels(m),
            // Admin endpoint: caller already has CanManageMenus, so they can edit any item.
            CanEdit: true,
            Children: childrenByParent.TryGetValue(m.Id, out var kids)
                ? kids.Select(c => ToDto(c, childrenByParent)).ToList()
                : new List<MenuItemAdminDto>(),
            Translations: m.Translations
                .Select(t => new MenuItemTranslationDto(t.LanguageCode, t.Label))
                .ToList());

    private static Dictionary<string, string> BuildLabels(MenuItem m) =>
        m.Translations.ToDictionary(t => t.LanguageCode, t => t.Label);
}
