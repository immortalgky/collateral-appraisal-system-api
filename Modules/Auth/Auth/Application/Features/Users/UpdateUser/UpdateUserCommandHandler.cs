using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.UpdateUser;

public class UpdateUserCommandHandler(UserManager<ApplicationUser> userManager)
    : ICommandHandler<UpdateUserCommand>
{
    public async Task<Unit> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.Id.ToString())
            ?? throw new NotFoundException("User", command.Id);

        user.FirstName = command.FirstName;
        user.LastName = command.LastName;
        user.Position = command.Position;
        user.Department = command.Department;
        user.CompanyId = command.CompanyId;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Unit.Value;
    }
}
