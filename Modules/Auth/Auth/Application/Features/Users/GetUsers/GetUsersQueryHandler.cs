using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Time;

namespace Auth.Application.Features.Users.GetUsers;

public class GetUsersQueryHandler(
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
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

        // Role filter: restrict to users who have a role with the given name
        if (!string.IsNullOrWhiteSpace(query.Role))
            q = q.Where(u => dbContext.UserRoles
                .Any(ur => ur.UserId == u.Id &&
                           dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == query.Role)));

        // Group filter: restrict to members of the given group
        if (query.GroupId.HasValue)
            q = q.Where(u => dbContext.GroupUsers
                .Any(gu => gu.UserId == u.Id && gu.GroupId == query.GroupId.Value));

        // Team filter: restrict to members of the given team
        if (query.TeamId.HasValue)
            q = q.Where(u => dbContext.TeamMembers
                .Any(tm => tm.UserId == u.Id && tm.TeamId == query.TeamId.Value));

        // Company filter: restrict to users belonging to the given company (Company-scoped users)
        if (query.CompanyId.HasValue)
            q = q.Where(u => u.CompanyId == query.CompanyId.Value);

        // Active status filter
        if (query.IsActive.HasValue)
            q = q.Where(u => u.IsActive == query.IsActive.Value);

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

        // Compare in UTC: LockoutEnd is a DateTimeOffset; comparing its UTC instant
        // against UtcNow avoids implicit local-offset conversion bugs.
        var nowUtc = dateTimeProvider.UtcNow;

        var items = users.Select(u => new UserListItemDto(
            u.Id, u.UserName ?? "", u.FirstName, u.LastName,
            u.Email, u.AvatarUrl, u.Position, u.Department, u.CompanyId, u.AuthSource,
            rolesByUser.TryGetValue(u.Id, out var roles) ? roles : [],
            u.IsActive,
            u.LockoutEnd.HasValue && u.LockoutEnd.Value.UtcDateTime > nowUtc));

        return new GetUsersResult(items, total, pageNumber, pageSize);
    }
}
