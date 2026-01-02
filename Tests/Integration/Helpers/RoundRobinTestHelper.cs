using System.Data;
using Dapper;
using Shared.Data;

namespace Integration.Helpers;

public static class RoundRobinTestHelper
{
    public static async Task CleanupRoundRobinAsync(ISqlConnectionFactory sqlConnectionFactory, string activityName, string groupsHash, CancellationToken ct = default)
    {
        using var conn = sqlConnectionFactory.GetOpenConnection();
        await conn.ExecuteAsync(
            "DELETE FROM [workflow].[RoundRobinQueue] WHERE [ActivityName] = @activity AND [GroupsHash] = @hash",
            new { activity = activityName, hash = groupsHash },
            commandType: CommandType.Text
        );
    }
}

