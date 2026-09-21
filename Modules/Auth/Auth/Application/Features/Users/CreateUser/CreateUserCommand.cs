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
    string? AoCode = null,
    // Bank staff employee id; only persisted for bank users (CompanyId == null).
    string? EmployeeId = null,
    // Ad-hoc account: created closed, no password, usable only inside an admin-opened access window.
    bool IsTemporaryAccess = false
) : ICommand<CreateUserResult>, ITransactionalCommand<IAuthUnitOfWork>;
