using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Data.Seed;
using Workflow.Domain.Committees;

namespace Workflow.Data.Seed;

/// <summary>
/// Seeds committees, members, and thresholds for committee approval voting.
/// Sub Committee (3 members): 0 - 50M
/// Committee Group 2 (5 members): 50M+
/// </summary>
public class CommitteeDataSeed(
    WorkflowDbContext context,
    ISqlConnectionFactory connectionFactory,
    ILogger<CommitteeDataSeed> logger) : IDataSeeder<WorkflowDbContext>
{
    public async Task SeedAllAsync()
    {
        await SeedCommitteesAsync();
        await SeedMembersAsync();
        await SeedThresholdsAsync();
    }

    private async Task SeedCommitteesAsync()
    {
        if (await context.Committees.AnyAsync())
        {
            logger.LogInformation("Workflow committees already seeded, skipping...");
            return;
        }

        logger.LogInformation("Seeding workflow committees...");

        var subCommittee = Committee.Create(
            "Sub Committee",
            "SUB_COMMITTEE",
            "Sub committee for appraisals up to 50M",
            QuorumType.Fixed,
            2,
            MajorityType.Unanimous);

        var committeeGroup2 = Committee.Create(
            "Committee Group 2",
            "COMMITTEE_GROUP_2",
            "Committee for appraisals above 50M",
            QuorumType.Fixed,
            3,
            MajorityType.Unanimous);

        context.Committees.Add(subCommittee);
        context.Committees.Add(committeeGroup2);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded 2 workflow committees");
    }

    private async Task SeedMembersAsync()
    {
        if (await context.Committees.AnyAsync(c => c.Members.Any()))
        {
            logger.LogInformation("Workflow committee members already seeded, skipping...");
            return;
        }

        var subCommittee = await context.Committees
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Code == "SUB_COMMITTEE");
        var committeeGroup2 = await context.Committees
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Code == "COMMITTEE_GROUP_2");

        if (subCommittee == null || committeeGroup2 == null)
        {
            logger.LogWarning("Workflow committees not found. Skipping member seeding.");
            return;
        }

        var userMap = await GetUserMapAsync();
        if (userMap.Count == 0)
        {
            logger.LogWarning("No users found in auth.AspNetUsers. Skipping member seeding.");
            return;
        }

        logger.LogInformation("Seeding workflow committee members...");

        // Sub Committee: 3 members
        AddMemberIfUserExists(subCommittee, userMap, "john.doe", "John Doe", CommitteeMemberRole.Chairman);
        AddMemberIfUserExists(subCommittee, userMap, "jane.smith", "Jane Smith", CommitteeMemberRole.UW);
        AddMemberIfUserExists(subCommittee, userMap, "m.wilson", "Mike Wilson", CommitteeMemberRole.Risk);

        // Committee Group 2: 5 members (all Sub Committee members + 2 more)
        AddMemberIfUserExists(committeeGroup2, userMap, "john.doe", "John Doe", CommitteeMemberRole.Chairman);
        AddMemberIfUserExists(committeeGroup2, userMap, "jane.smith", "Jane Smith", CommitteeMemberRole.UW);
        AddMemberIfUserExists(committeeGroup2, userMap, "m.wilson", "Mike Wilson", CommitteeMemberRole.Risk);
        AddMemberIfUserExists(committeeGroup2, userMap, "s.johnson", "Sarah Johnson", CommitteeMemberRole.Credit);
        AddMemberIfUserExists(committeeGroup2, userMap, "thitipornw", "Thitiporn W", CommitteeMemberRole.Appraisal);

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded workflow committee members: Sub Committee={SubCount}, Group 2={Group2Count}",
            subCommittee.Members.Count, committeeGroup2.Members.Count);
    }

    private async Task SeedThresholdsAsync()
    {
        if (await context.Committees.AnyAsync(c => c.Thresholds.Any()))
        {
            logger.LogInformation("Workflow committee thresholds already seeded, skipping...");
            return;
        }

        var subCommittee = await context.Committees
            .FirstOrDefaultAsync(c => c.Code == "SUB_COMMITTEE");
        var committeeGroup2 = await context.Committees
            .FirstOrDefaultAsync(c => c.Code == "COMMITTEE_GROUP_2");

        if (subCommittee == null || committeeGroup2 == null)
        {
            logger.LogWarning("Workflow committees not found. Skipping threshold seeding.");
            return;
        }

        logger.LogInformation("Seeding workflow committee thresholds...");

        subCommittee.AddThreshold(0m, 50_000_000m, 1);
        committeeGroup2.AddThreshold(50_000_000m, null, 2);

        await context.SaveChangesAsync();

        logger.LogInformation("Seeded 2 workflow committee thresholds");
    }

    private async Task<Dictionary<string, Guid>> GetUserMapAsync()
    {
        const string sql = "SELECT Id, UserName FROM auth.AspNetUsers WHERE UserName IS NOT NULL";
        var connection = connectionFactory.GetOpenConnection();
        var users = await connection.QueryAsync<(Guid Id, string UserName)>(sql);
        return users.ToDictionary(u => u.UserName, u => u.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddMemberIfUserExists(
        Committee committee,
        Dictionary<string, Guid> userMap,
        string username,
        string displayName,
        CommitteeMemberRole role)
    {
        if (userMap.TryGetValue(username, out _))
        {
            committee.AddMember(username, displayName, role);
        }
    }
}
