using Auth.Application.Configurations;

namespace Auth.Application.Features.Users.CreateUser;

public record CreateUserCommand(
    string Username,
    string Password,
    string Email,
    string FirstName,
    string LastName,
    string? Position,
    string? Department,
    Guid? CompanyId,
    List<Guid> Roles,
    string AuthSource = AuthSources.Local,
    List<Guid>? GroupIds = null,
    List<Guid>? TeamIds = null,
    // Bank-internal officer code; only persisted for bank users (CompanyId == null).
    string? AoCode = null
) : ICommand<CreateUserResult>, ITransactionalCommand<IAuthUnitOfWork>;
