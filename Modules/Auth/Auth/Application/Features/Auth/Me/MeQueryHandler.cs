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

        return new MeResult(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email,
            user.FirstName,
            user.LastName,
            user.AvatarUrl,
            user.Position,
            user.Department,
            user.CompanyId,
            roles.ToList(),
            permissions,
            groups
        );
    }
}
