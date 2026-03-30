using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.UpdateUserRoles;

public class UpdateUserRolesCommandHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager)
    : ICommandHandler<UpdateUserRolesCommand>
{
    public async Task<Unit> Handle(UpdateUserRolesCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        // Validate all role names exist
        foreach (var roleName in command.RoleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                throw new NotFoundException("Role", roleName);
        }

        var currentRoles = await userManager.GetRolesAsync(user);

        // Remove roles no longer in list
        var toRemove = currentRoles.Except(command.RoleNames).ToList();
        if (toRemove.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", removeResult.Errors.Select(e => e.Description)));
        }

        // Add new roles
        var toAdd = command.RoleNames.Except(currentRoles).ToList();
        if (toAdd.Count > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", addResult.Errors.Select(e => e.Description)));
        }

        return Unit.Value;
    }
}
