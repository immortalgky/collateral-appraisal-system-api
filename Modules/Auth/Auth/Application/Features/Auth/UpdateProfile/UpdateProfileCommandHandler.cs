using Microsoft.AspNetCore.Identity;
using Auth.Domain.Identity;
using Shared.Exceptions;

namespace Auth.Domain.Auth.Features.UpdateProfile;

public class UpdateProfileCommandHandler(
    UserManager<ApplicationUser> userManager
) : ICommandHandler<UpdateProfileCommand, UpdateProfileResult>
{
    public async Task<UpdateProfileResult> Handle(
        UpdateProfileCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
                   ?? throw new NotFoundException("User", command.UserId);

        user.FirstName = command.FirstName;
        user.LastName = command.LastName;
        user.AvatarUrl = command.AvatarUrl;
        user.Position = command.Position;
        user.Department = command.Department;

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(e => e.Description)));

        return new UpdateProfileResult(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email,
            user.FirstName,
            user.LastName,
            user.AvatarUrl,
            user.Position,
            user.Department
        );
    }
}