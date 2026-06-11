using Auth.Application.Configurations;

namespace Auth.Application.Features.Roles.UpdateRoleUsers;

// Transactional: RoleService.UpdateRoleUsers loops UserManager.RemoveFromRoleAsync /
// AddToRoleAsync, each of which auto-saves (Identity). Without one ambient transaction a
// failure mid-loop leaves the role half-reassigned. ITransactionalCommand<IAuthUnitOfWork>
// makes the whole reassignment atomic.
public record UpdateRoleUsersCommand(Guid RoleId, List<Guid> UserIds)
    : ICommand<UpdateRoleUsersResult>, ITransactionalCommand<IAuthUnitOfWork>;
