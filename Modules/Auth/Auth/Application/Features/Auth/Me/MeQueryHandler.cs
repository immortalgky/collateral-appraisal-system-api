using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Auth.Application.Services;
using Auth.Infrastructure;
using Auth.Domain.Identity;
using Shared.Exceptions;

namespace Auth.Domain.Auth.Features.Me;

public class MeQueryHandler(
    AuthDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    PermissionResolver permissionResolver
) : IQueryHandler<MeQuery, MeResult>
{
    public async Task<MeResult> Handle(MeQuery query, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
                       .Include(u => u.Permissions)
                       .ThenInclude(up => up.Permission)
                       .FirstOrDefaultAsync(u => u.Id == query.UserId, cancellationToken)
                   ?? throw new NotFoundException("User", query.UserId);

        var roles = await userManager.GetRolesAsync(user);
        var permissions = (await permissionResolver.CalculateAsync(user, roles)).ToList();

        var groups = await (
            from gu in dbContext.GroupUsers
            join g in dbContext.Groups on gu.GroupId equals g.Id
            where gu.UserId == query.UserId
            select new MeGroupDto(g.Id, g.Name, g.Scope)
        ).ToListAsync(cancellationToken);

        // Teams the user belongs to (auth.TeamMembers → auth.Teams), mirroring GetUserByIdQueryHandler.
        var teams = await (
            from tm in dbContext.TeamMembers
            join t in dbContext.Teams on tm.TeamId equals t.Id
            where tm.UserId == query.UserId
            select new MeTeamDto(t.Id, t.Name, t.Scope)
        ).ToListAsync(cancellationToken);

        // Company name is only meaningful for external users; both languages ride along
        // so the client can pick by its own locale.
        string? companyName = null;
        string? companyNameLocal = null;
        if (user.CompanyId.HasValue)
        {
            var company = await dbContext.Companies
                .Where(c => c.Id == user.CompanyId.Value)
                .Select(c => new { c.Name, c.NameLocal })
                .FirstOrDefaultAsync(cancellationToken);
            companyName = company?.Name;
            companyNameLocal = string.IsNullOrWhiteSpace(company?.NameLocal) ? null : company!.NameLocal;
        }

        return new MeResult(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email,
            user.FirstName,
            user.LastName,
            user.AvatarUrl,
            user.Position,
            user.Department,
            user.AoCode,
            user.EmployeeId,
            user.CompanyId,
            companyName,
            companyNameLocal,
            user.AuthSource,
            user.IsActive,
            user.LastLoginAt,
            user.PasswordChangedAt,
            roles.ToList(),
            permissions,
            groups,
            teams,
            user.MustChangePassword
        );
    }
}
