using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Features.Users.GetUsers;

public class GetUsersQueryHandler(UserManager<ApplicationUser> userManager, AuthDbContext dbContext)
    : IQueryHandler<GetUsersQuery, GetUsersResult>
{
    public async Task<GetUsersResult> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        var q = userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(u =>
                u.UserName!.Contains(query.Search) ||
                u.FirstName.Contains(query.Search) ||
                u.LastName.Contains(query.Search) ||
                (u.Email != null && u.Email.Contains(query.Search)));

        // Scope: Bank users have no CompanyId, Company users do
        if (query.Scope == "Bank")
            q = q.Where(u => u.CompanyId == null);
        else if (query.Scope == "Company")
            q = q.Where(u => u.CompanyId != null);

        var total = await q.LongCountAsync(cancellationToken);

        var pageSize = query.PageSize;
        var pageNumber = query.PageNumber;

        var users = await q
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip(Math.Max(pageNumber - 1, 0) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
            return new GetUsersResult([], total, pageNumber, pageSize);

        // Load all user-role mappings for this page in one query
        var userIds = users.Select(u => u.Id).ToList();
        var userRoles = await (
            from ur in dbContext.UserRoles
            join r in dbContext.Roles on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name ?? "" }
        ).ToListAsync(cancellationToken);

        var rolesByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

        var items = users.Select(u => new UserListItemDto(
            u.Id, u.UserName ?? "", u.FirstName, u.LastName,
            u.Email, u.AvatarUrl, u.Position, u.Department, u.CompanyId, u.AuthSource,
            rolesByUser.TryGetValue(u.Id, out var roles) ? roles : []));

        return new GetUsersResult(items, total, pageNumber, pageSize);
    }
}
