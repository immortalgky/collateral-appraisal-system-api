using Auth.Application.Features.Menu.Dtos;
using Auth.Application.Services;
using Auth.Domain.Menu;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Auth.Application.Features.Menu.GetMyMenu;

public class GetMyMenuQueryHandler(
    AuthDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    PermissionResolver permissionResolver,
    IMenuTreeCache menuTreeCache
) : IQueryHandler<GetMyMenuQuery, MyMenuResponse>
{
    public async Task<MyMenuResponse> Handle(GetMyMenuQuery query, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
                       .Include(u => u.Permissions)
                       .ThenInclude(up => up.Permission)
                       .FirstOrDefaultAsync(u => u.Id == query.UserId, cancellationToken)
                   ?? throw new NotFoundException("User", query.UserId);

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await permissionResolver.CalculateAsync(user, roles);

        var allItems = await menuTreeCache.GetAllAsync(cancellationToken);

        // Activity overrides only affect the appraisal scope. When the caller passes an
        // activityId (e.g. user opened a task for "provide-additional-documents"), the
        // override rows become the authoritative source of visibility/editability for
        // appraisal-scope items — bypassing the role permission check. This lets roles
        // like RequestMaker (which don't hold appraisal section view perms) still see
        // the task-relevant menu items for the duration of the activity.
        Dictionary<Guid, ActivityMenuOverride> appraisalOverrides = new();
        if (!string.IsNullOrWhiteSpace(query.ActivityId))
        {
            appraisalOverrides = await dbContext.ActivityMenuOverrides
                .AsNoTracking()
                .Where(o => o.ActivityId == query.ActivityId)
                .ToDictionaryAsync(o => o.MenuItemId, cancellationToken);
        }

        var mainTree = BuildFilteredTree(allItems, MenuScope.Main, permissions, overrides: null);
        var appraisalTree = BuildFilteredTree(allItems, MenuScope.Appraisal, permissions, appraisalOverrides);

        return new MyMenuResponse(mainTree, appraisalTree);
    }

    private static List<MenuTreeNodeDto> BuildFilteredTree(
        IReadOnlyList<MenuItem> allItems,
        MenuScope scope,
        HashSet<string> permissions,
        IReadOnlyDictionary<Guid, ActivityMenuOverride>? overrides)
    {
        var scoped = allItems.Where(i => i.Scope == scope).ToList();
        var childrenByParent = scoped
            .GroupBy(i => i.ParentId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.OrderBy(i => i.SortOrder).ToList());

        List<MenuTreeNodeDto> BuildLevel(Guid parentKey)
        {
            if (!childrenByParent.TryGetValue(parentKey, out var siblings))
                return new List<MenuTreeNodeDto>();

            var result = new List<MenuTreeNodeDto>();
            foreach (var item in siblings)
            {
                bool isVisible;
                bool canEdit;

                if (overrides is not null && overrides.TryGetValue(item.Id, out var ov))
                {
                    // Override path: activity config wins.
                    isVisible = ov.IsVisible;
                    canEdit = ov.CanEdit;
                }
                else
                {
                    // Default path: role permissions.
                    isVisible = permissions.Contains(item.ViewPermissionCode);
                    canEdit = !string.IsNullOrEmpty(item.EditPermissionCode)
                              && permissions.Contains(item.EditPermissionCode!);
                }

                if (!isVisible)
                    continue;

                var children = BuildLevel(item.Id);

                var labels = item.Translations.ToDictionary(t => t.LanguageCode, t => t.Label);

                result.Add(new MenuTreeNodeDto(
                    item.Id,
                    item.ItemKey,
                    item.Path,
                    item.Icon.Name,
                    item.Icon.Style.ToString().ToLowerInvariant(),
                    item.IconColor,
                    item.SortOrder,
                    labels,
                    canEdit,
                    children));
            }

            return result;
        }

        return BuildLevel(Guid.Empty);
    }
}
