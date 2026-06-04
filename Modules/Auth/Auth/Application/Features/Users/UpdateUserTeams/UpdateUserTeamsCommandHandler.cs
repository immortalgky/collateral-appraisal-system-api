using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Teams;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.UpdateUserTeams;

public class UpdateUserTeamsCommandHandler(
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext,
    IAuthAuditWriter auditWriter)
    : ICommandHandler<UpdateUserTeamsCommand>
{
    public async Task<Unit> Handle(UpdateUserTeamsCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        var requestedTeamIds = (command.TeamIds ?? []).Distinct().ToList();

        var existingTeamIds = await dbContext.Teams
            .Where(t => requestedTeamIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var missing = requestedTeamIds.Except(existingTeamIds).ToList();
        if (missing.Count > 0)
            throw new NotFoundException("Team", missing[0]);

        var currentLinks = await dbContext.TeamMembers
            .Where(m => m.UserId == command.UserId)
            .ToListAsync(cancellationToken);

        var currentTeamIds = currentLinks.Select(l => l.TeamId).ToList();

        var toRemove = currentLinks.Where(l => !requestedTeamIds.Contains(l.TeamId)).ToList();
        if (toRemove.Count > 0)
            dbContext.TeamMembers.RemoveRange(toRemove);

        foreach (var teamId in requestedTeamIds.Except(currentTeamIds))
            dbContext.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = command.UserId });

        auditWriter.RecordAssignmentChange(
            AuditEntityType.User,
            command.UserId,
            user.UserName,
            currentTeamIds,
            requestedTeamIds,
            "teams");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
