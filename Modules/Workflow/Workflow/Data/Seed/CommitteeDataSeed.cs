using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Data.Seed;
using Workflow.Domain.Committees;

namespace Workflow.Data.Seed;

/// <summary>
/// Seeds committees, members, thresholds and conditions for committee approval voting.
/// 3-tier approval routing by facilityLimit:
///   - Sub Committee (SUB_COMMITTEE):          0 - 10M
///   - Committee (COMMITTEE):                 10M - 30M
///   - Committee With Meeting (COMMITTEE_WITH_MEETING): >30M, UW role vote required
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
        await SeedConditionsAsync();
    }

    private async Task SeedCommitteesAsync()
    {
        // Guard: skip only when all three expected committees exist.
        // A partial seed (e.g., an older deployment that only inserted SUB_COMMITTEE/COMMITTEE)
        // must be allowed to top up with COMMITTEE_WITH_MEETING — otherwise the tier-3 routing
        // introduced by the meeting-approval feature silently fails because the committee the
        // workflow expects is missing.
        var existingCodes = await context.Committees
            .Select(c => c.Code)
            .ToListAsync();

        var requiredCodes = new[] { "SUB_COMMITTEE", "COMMITTEE", "COMMITTEE_WITH_MEETING" };
        if (requiredCodes.All(existingCodes.Contains))
        {
            logger.LogInformation("Workflow committees already seeded, skipping...");
            return;
        }

        if (existingCodes.Count > 0)
        {
            logger.LogWarning(
                "Partial committee seed detected (existing: {Existing}). Skipping top-up to avoid duplicating members/thresholds. " +
                "Manually add the missing committees: {Missing}",
                string.Join(",", existingCodes),
                string.Join(",", requiredCodes.Except(existingCodes)));
            return;
        }

        logger.LogInformation("Seeding workflow committees...");

        var subCommittee = Committee.Create(
            "Sub Committee",
            "SUB_COMMITTEE",
            "Sub committee for appraisals up to 10M",
            QuorumType.Fixed,
            2,
            MajorityType.Unanimous);

        var committee = Committee.Create(
            "Committee",
            "COMMITTEE",
            "Committee for appraisals between 10M and 30M",
            QuorumType.Fixed,
            3,
            MajorityType.Unanimous);

        var committeeWithMeeting = Committee.Create(
            "Committee With Meeting",
            "COMMITTEE_WITH_MEETING",
            "Committee for appraisals above 30M — requires a meeting and UW vote",
            QuorumType.Fixed,
            3,
            MajorityType.Unanimous);

        context.Committees.Add(subCommittee);
        context.Committees.Add(committee);
        context.Committees.Add(committeeWithMeeting);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded 3 workflow committees");
    }

    private async Task SeedMembersAsync()
    {
        if (await context.Committees.AnyAsync(c => c.Members.Any()))
        {
            logger.LogInformation("Workflow committee members already seeded, skipping...");
            return;
        }

        var subCommittee = await context.Committees.Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Code == "SUB_COMMITTEE");
        var committee = await context.Committees.Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Code == "COMMITTEE");
        var committeeWithMeeting = await context.Committees.Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Code == "COMMITTEE_WITH_MEETING");

        if (subCommittee is null || committee is null || committeeWithMeeting is null)
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

        // Sub Committee: 3 members (tier 1: 0-10M)
        AddMemberIfUserExists(subCommittee, userMap, "john.doe", "John Doe", CommitteeMemberPosition.Chairman);
        AddMemberIfUserExists(subCommittee, userMap, "jane.smith", "Jane Smith", CommitteeMemberPosition.UW);
        AddMemberIfUserExists(subCommittee, userMap, "m.wilson", "Mike Wilson", CommitteeMemberPosition.Risk);

        // Committee: 5 members (tier 2: 10-30M)
        AddMemberIfUserExists(committee, userMap, "john.doe", "John Doe", CommitteeMemberPosition.Chairman);
        AddMemberIfUserExists(committee, userMap, "jane.smith", "Jane Smith", CommitteeMemberPosition.UW);
        AddMemberIfUserExists(committee, userMap, "m.wilson", "Mike Wilson", CommitteeMemberPosition.Risk);
        AddMemberIfUserExists(committee, userMap, "s.johnson", "Sarah Johnson", CommitteeMemberPosition.Credit);
        AddMemberIfUserExists(committee, userMap, "thitipornw", "Thitiporn W", CommitteeMemberPosition.Appraisal);

        // Committee With Meeting: 5 members (tier 3: >30M) — UW vote mandatory
        AddMemberIfUserExists(committeeWithMeeting, userMap, "john.doe", "John Doe", CommitteeMemberPosition.Chairman);
        AddMemberIfUserExists(committeeWithMeeting, userMap, "jane.smith", "Jane Smith", CommitteeMemberPosition.UW);
        AddMemberIfUserExists(committeeWithMeeting, userMap, "m.wilson", "Mike Wilson", CommitteeMemberPosition.Risk);
        AddMemberIfUserExists(committeeWithMeeting, userMap, "s.johnson", "Sarah Johnson", CommitteeMemberPosition.Credit);
        AddMemberIfUserExists(committeeWithMeeting, userMap, "thitipornw", "Thitiporn W", CommitteeMemberPosition.Appraisal);

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded committee members: Sub={Sub}, Committee={C}, WithMeeting={WM}",
            subCommittee.Members.Count, committee.Members.Count, committeeWithMeeting.Members.Count);
    }

    private async Task SeedThresholdsAsync()
    {
        if (await context.Committees.AnyAsync(c => c.Thresholds.Any()))
        {
            logger.LogInformation("Workflow committee thresholds already seeded, skipping...");
            return;
        }

        var subCommittee = await context.Committees.FirstOrDefaultAsync(c => c.Code == "SUB_COMMITTEE");
        var committee = await context.Committees.FirstOrDefaultAsync(c => c.Code == "COMMITTEE");
        var committeeWithMeeting = await context.Committees.FirstOrDefaultAsync(c => c.Code == "COMMITTEE_WITH_MEETING");

        if (subCommittee is null || committee is null || committeeWithMeeting is null)
        {
            logger.LogWarning("Workflow committees not found. Skipping threshold seeding.");
            return;
        }

        logger.LogInformation("Seeding workflow committee thresholds...");

        subCommittee.AddThreshold(0m, 10_000_000m, 1);
        committee.AddThreshold(10_000_000m, 30_000_000m, 2);
        committeeWithMeeting.AddThreshold(30_000_000m, null, 3);

        await context.SaveChangesAsync();

        logger.LogInformation("Seeded 3 workflow committee thresholds");
    }

    private async Task SeedConditionsAsync()
    {
        var committeeWithMeeting = await context.Committees
            .Include(c => c.Conditions)
            .FirstOrDefaultAsync(c => c.Code == "COMMITTEE_WITH_MEETING");

        if (committeeWithMeeting is null)
            return;

        if (committeeWithMeeting.Conditions.Any())
            return;

        logger.LogInformation("Seeding COMMITTEE_WITH_MEETING approval condition (UW role required)...");

        committeeWithMeeting.AddCondition(
            ConditionType.RoleRequired,
            roleRequired: nameof(CommitteeMemberPosition.UW),
            minVotesRequired: null,
            priority: 1,
            description: "Underwriter (UW) must cast an approve vote for tier-3 appraisals");

        await context.SaveChangesAsync();
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
        CommitteeMemberPosition position)
    {
        if (userMap.TryGetValue(username, out _))
        {
            committee.AddMember(username, displayName, position);
        }
    }
}
