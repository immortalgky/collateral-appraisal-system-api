using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Groups;
using Auth.Domain.Teams;
using Auth.Infrastructure;
using Auth.Services;

namespace Auth.Application.Features.Users.CreateUser;

// ITransactionalCommand<IAuthUnitOfWork> wraps this whole handler in one transaction (see
// TransactionalBehavior): the user, role links, password history, and group/team links all
// commit together or roll back together. The behavior owns SaveChanges + Commit, so this
// handler must NOT call SaveChangesAsync itself.
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
        var groupIds = (command.GroupIds ?? []).Distinct().ToList();
        var teamIds = (command.TeamIds ?? []).Distinct().ToList();

        // Validate optional references up front so a bad id fails the request before anything is
        // written (and inside the ambient transaction, so the rollback has nothing to undo).
        await UserAssignmentValidator.ValidateGroupsExistAsync(dbContext, groupIds, cancellationToken);
        await UserAssignmentValidator.ValidateTeamsExistAsync(dbContext, teamIds, cancellationToken);

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
            Roles: command.Roles,
            AuthSource: command.AuthSource,
            // Bank-internal attribute — only carry it for bank users (no company).
            AoCode: command.CompanyId is null ? command.AoCode : null);

        var user = await registrationService.RegisterUser(registerUserDto, cancellationToken);

        foreach (var groupId in groupIds)
            dbContext.GroupUsers.Add(new GroupUser { GroupId = groupId, UserId = user.Id });

        foreach (var teamId in teamIds)
            dbContext.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = user.Id });

        auditWriter.Record(AuditAction.Created, AuditEntityType.User, user.Id, command.Username);

        return new CreateUserResult(user.Id);
    }
}
