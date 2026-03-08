using Dapper;
using Shared.Data.Seed;

namespace Appraisal.Infrastructure.Seed;

/// <summary>
/// Seeds committees, members, and thresholds for committee approval voting.
/// Sub Committee (3 members): 0 - 50M
/// Committee Group 2 (5 members): 50M+
/// </summary>
public class CommitteeThresholdDataSeed : IDataSeeder<AppraisalDbContext>
{
    private readonly AppraisalDbContext _context;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<CommitteeThresholdDataSeed> _logger;

    public CommitteeThresholdDataSeed(
        AppraisalDbContext context,
        ISqlConnectionFactory connectionFactory,
        ILogger<CommitteeThresholdDataSeed> logger)
    {
        _context = context;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task SeedAllAsync()
    {
        await SeedCommitteesAsync();
        await SeedMembersAsync();
        await SeedThresholdsAsync();
    }

    private async Task SeedCommitteesAsync()
    {
        if (await _context.Committees.AnyAsync())
        {
            _logger.LogInformation("Committees already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding committees...");

        var subCommittee = Committee.Create(
            "Sub Committee",
            "SUB_COMMITTEE",
            "Fixed",
            2,
            "Unanimous");

        var committeeGroup2 = Committee.Create(
            "Committee Group 2",
            "COMMITTEE_GROUP_2",
            "Fixed",
            3,
            "Unanimous");

        _context.Committees.Add(subCommittee);
        _context.Committees.Add(committeeGroup2);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded 2 committees");
    }

    private async Task SeedMembersAsync()
    {
        // Check if members already exist
        if (await _context.CommitteeMembers.AnyAsync())
        {
            _logger.LogInformation("Committee members already seeded, skipping...");
            return;
        }

        var subCommittee = await _context.Committees
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.CommitteeCode == "SUB_COMMITTEE");
        var committeeGroup2 = await _context.Committees
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.CommitteeCode == "COMMITTEE_GROUP_2");

        if (subCommittee == null || committeeGroup2 == null)
        {
            _logger.LogWarning("Committees not found. Skipping member seeding.");
            return;
        }

        // Look up seeded user IDs from auth schema
        var userMap = await GetUserMapAsync();
        if (userMap.Count == 0)
        {
            _logger.LogWarning("No users found in auth.AspNetUsers. Skipping member seeding.");
            return;
        }

        _logger.LogInformation("Seeding committee members...");

        // Sub Committee: 3 members
        AddMemberIfUserExists(subCommittee, userMap, "john.doe", "John Doe", "Chairman");
        AddMemberIfUserExists(subCommittee, userMap, "jane.smith", "Jane Smith", "UW");
        AddMemberIfUserExists(subCommittee, userMap, "mike.wilson", "Mike Wilson", "Risk");

        // Committee Group 2: 5 members (all Sub Committee members + 2 more)
        AddMemberIfUserExists(committeeGroup2, userMap, "john.doe", "John Doe", "Chairman");
        AddMemberIfUserExists(committeeGroup2, userMap, "jane.smith", "Jane Smith", "UW");
        AddMemberIfUserExists(committeeGroup2, userMap, "mike.wilson", "Mike Wilson", "Risk");
        AddMemberIfUserExists(committeeGroup2, userMap, "sarah.johnson", "Sarah Johnson", "Credit");
        AddMemberIfUserExists(committeeGroup2, userMap, "thitipornw", "Thitiporn W", "Appraisal");

        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded committee members: Sub Committee={SubCount}, Group 2={Group2Count}",
            subCommittee.Members.Count, committeeGroup2.Members.Count);
    }

    private async Task SeedThresholdsAsync()
    {
        if (await _context.CommitteeThresholds.AnyAsync())
        {
            _logger.LogInformation("Committee thresholds already seeded, skipping...");
            return;
        }

        var subCommittee = await _context.Committees
            .FirstOrDefaultAsync(c => c.CommitteeCode == "SUB_COMMITTEE");
        var committeeGroup2 = await _context.Committees
            .FirstOrDefaultAsync(c => c.CommitteeCode == "COMMITTEE_GROUP_2");

        if (subCommittee == null || committeeGroup2 == null)
        {
            _logger.LogWarning("Committees not found. Skipping threshold seeding.");
            return;
        }

        _logger.LogInformation("Seeding committee thresholds...");

        var thresholds = new List<CommitteeThreshold>
        {
            CommitteeThreshold.Create(subCommittee.Id, 0m, 50_000_000m, 1),
            CommitteeThreshold.Create(committeeGroup2.Id, 50_000_000m, null, 2)
        };

        _context.CommitteeThresholds.AddRange(thresholds);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} committee thresholds", thresholds.Count);
    }

    private async Task<Dictionary<string, Guid>> GetUserMapAsync()
    {
        const string sql = "SELECT Id, UserName FROM auth.AspNetUsers WHERE UserName IS NOT NULL";
        var connection = _connectionFactory.GetOpenConnection();
        var users = await connection.QueryAsync<(Guid Id, string UserName)>(sql);
        return users.ToDictionary(u => u.UserName, u => u.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddMemberIfUserExists(
        Committee committee,
        Dictionary<string, Guid> userMap,
        string username,
        string displayName,
        string role)
    {
        if (userMap.TryGetValue(username, out var userId))
        {
            committee.AddMember(userId, displayName, role);
        }
    }
}
