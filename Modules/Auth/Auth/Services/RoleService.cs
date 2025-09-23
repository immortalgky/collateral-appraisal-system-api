using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OAuth2OpenId.Identity.Models;
using Shared.Exceptions;

namespace Auth.Services;

public class RoleService(RoleManager<ApplicationRole> roleManager) : IRoleService
{
    public async Task<ApplicationRole> CreateRole(
        RoleDto roleDto,
        CancellationToken cancellationToken = default
    )
    {
        var role = new ApplicationRole { Name = roleDto.Name, Description = roleDto.Description };
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(
                    "; ",
                    result.Errors.Select(error => $"{error.Code}: {error.Description}")
                )
            );
        }
        return role;
    }

    public async Task<PaginatedResult<ApplicationRole>> GetRoles(
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default
    )
    {
        var roles = roleManager.Roles.Include(role => role.Permissions);
        var requests = await PaginationExtensions.ToPaginatedResultAsync(
            roles,
            paginationRequest,
            cancellationToken
        );

        return requests;
    }

    public async Task<ApplicationRole?> GetRoleById(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var role = await roleManager
            .Roles.Include(role => role.Permissions)
            .FirstOrDefaultAsync(role => role.Id == id, cancellationToken: cancellationToken);

        return role;
    }

    public async Task DeleteRole(Guid id, CancellationToken cancellationToken = default)
    {
        var role =
            await GetRoleById(id, cancellationToken)
            ?? throw new NotFoundException("Cannot find application role with this ID.");
        await roleManager.DeleteAsync(role);
    }
}
