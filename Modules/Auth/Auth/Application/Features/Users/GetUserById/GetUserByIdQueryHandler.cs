using Auth.Domain.Groups;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Time;

namespace Auth.Application.Features.Users.GetUserById;

public class GetUserByIdQueryHandler(
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetUserByIdQuery, GetUserByIdResult>
{
    public async Task<GetUserByIdResult> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.Permissions).ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException("User", query.Id);

        var roleNames = await userManager.GetRolesAsync(user);
        var roles = await dbContext.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => new UserRoleDto(r.Id, r.Name!, r.Scope))
            .ToListAsync(cancellationToken);

        // Load groups the user belongs to
        var groups = await (
            from gu in dbContext.GroupUsers
            join g in dbContext.Groups on gu.GroupId equals g.Id
            where gu.UserId == query.Id
            select new UserGroupDto(g.Id, g.Name, g.Scope)
        ).ToListAsync(cancellationToken);

        // Load teams the user belongs to (via auth.TeamMembers → auth.Teams)
        var teams = await (
            from tm in dbContext.TeamMembers
            join t in dbContext.Teams on tm.TeamId equals t.Id
            where tm.UserId == query.Id
            select new UserTeamDto(t.Id, t.Name, t.Scope)
        ).ToListAsync(cancellationToken);

        var permissions = user.Permissions
            .Where(up => up.Permission != null)
            .Select(up => new UserPermissionDto(up.Permission.Id, up.Permission.PermissionCode, up.IsGranted))
            .ToList();

        // Resolve company name (null for bank-internal users who have no CompanyId)
        string? companyName = null;
        if (user.CompanyId.HasValue)
            companyName = await dbContext.Companies
                .Where(c => c.Id == user.CompanyId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);

        // Compare in UTC: LockoutEnd is a DateTimeOffset; comparing its UTC instant
        // against UtcNow avoids implicit local-offset conversion bugs.
        var nowUtc = dateTimeProvider.UtcNow;
        var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value.UtcDateTime > nowUtc;

        return new GetUserByIdResult(
            user.Id,
            user.UserName ?? "",
            user.FirstName,
            user.LastName,
            user.Email,
            user.AvatarUrl,
            user.Position,
            user.Department,
            user.CompanyId,
            companyName,
            user.AuthSource,
            user.IsActive,
            isLocked,
            user.LastLoginAt,
            roles,
            groups,
            teams,
            permissions);
    }
}
