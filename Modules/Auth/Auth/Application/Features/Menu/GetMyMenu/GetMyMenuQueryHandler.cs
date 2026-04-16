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

        var mainTree = BuildFilteredTree(allItems, MenuScope.Main, permissions);
        var appraisalTree = BuildFilteredTree(allItems, MenuScope.Appraisal, permissions);

        return new MyMenuResponse(mainTree, appraisalTree);
    }

    private static List<MenuTreeNodeDto> BuildFilteredTree(
        IReadOnlyList<MenuItem> allItems,
        MenuScope scope,
        HashSet<string> permissions)
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
                if (!permissions.Contains(item.ViewPermissionCode))
                    continue;

                var children = BuildLevel(item.Id);
                var canEdit = !string.IsNullOrEmpty(item.EditPermissionCode)
                              && permissions.Contains(item.EditPermissionCode!);

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
