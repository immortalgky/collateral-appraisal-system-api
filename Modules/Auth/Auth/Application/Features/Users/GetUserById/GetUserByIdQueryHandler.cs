using Auth.Domain.Groups;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.GetUserById;

public class GetUserByIdQueryHandler(UserManager<ApplicationUser> userManager, AuthDbContext dbContext)
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

        var permissions = user.Permissions
            .Where(up => up.Permission != null)
            .Select(up => new UserPermissionDto(up.Permission.Id, up.Permission.PermissionCode, up.IsGranted))
            .ToList();

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
            user.AuthSource,
            roles,
            groups,
            permissions);
    }
}
