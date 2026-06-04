using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure;
using Auth.Services;

namespace Auth.Application.Features.Users.CreateUser;

public class CreateUserCommandHandler(
    IRegistrationService registrationService,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
    : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var registerUserDto = new RegisterUserDto(
            Username: command.Username,
            Password: command.Password,
            Email: command.Email,
            FirstName: command.FirstName,
            LastName: command.LastName,
            AvatarUrl: null,
            Position: command.Position,
            Department: command.Department,
            CompanyId: command.CompanyId,
            Permissions: [],
            Roles: command.Roles);

        var user = await registrationService.RegisterUser(registerUserDto, cancellationToken);

        auditWriter.Record(AuditAction.Created, AuditEntityType.User, user.Id, command.Username);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateUserResult(user.Id);
    }
}
