using System.Text.Json;
using FluentAssertions;
using Integration.FailedMessages;

namespace Integration.Tests;

/// <summary>
/// These tests drive <see cref="FailedMessageCollectorService.ParseManagementQueues"/>
/// directly — no broker or HTTP call needed to prove either the discovery security filter or the
/// null-tolerance of the Management API payload parsing.
/// </summary>
public class FailedMessageManagementQueuesParsingTests
{
    /// <summary>
    /// SECURITY/data: the Management API is a whole-vhost read — another application's fault queue
    /// must never become a candidate, even though it's right there in the same response with messages
    /// waiting. Only queues matching OUR own endpoint names (the observer plus the ordered seed) count.
    /// </summary>
    [Fact]
    public void ParseManagementQueues_FiltersFaultQueuesToOurEndpointsOnly()
    {
        const string payload = """
            [
              {"name":"their-consumer_error","messages":5},
              {"name":"appraisal-sync_error","messages":2}
            ]
            """;
        var ourFaultQueues = new HashSet<string>(StringComparer.Ordinal) { "appraisal-sync_error", "appraisal-sync_skipped" };

        var (faultQueues, _) = FailedMessageCollectorService.ParseManagementQueues(payload, ourFaultQueues);

        faultQueues.Should().ContainSingle().Which.Should().Be("appraisal-sync_error");
    }

    /// <summary>
    /// SECURITY/data: the snapshot's QueuesJson is shown to every FAILED_MESSAGE_VIEW user, so it must
    /// carry only OUR endpoints' queues — never another application's names, depths or rates from the
    /// shared vhost. "Ours" is the same endpoint set that scopes the fault-queue list.
    /// </summary>
    [Fact]
    public void ParseManagementQueues_SnapshotQueuesJson_ContainsOnlyOurEndpoints()
    {
        const string payload = """
            [
              {"name":"appraisal-sync","messages_ready":4},
              {"name":"their-consumer","messages_ready":99},
              {"name":"their-consumer_error","messages":5}
            ]
            """;
        var ourFaultQueues = new HashSet<string>(StringComparer.Ordinal) { "appraisal-sync_error", "appraisal-sync_skipped" };

        var (_, queuesJson) = FailedMessageCollectorService.ParseManagementQueues(payload, ourFaultQueues);

        using var doc = JsonDocument.Parse(queuesJson);
        doc.RootElement.EnumerateArray().Select(q => q.GetProperty("name").GetString())
            .Should().ContainSingle().Which.Should().Be("appraisal-sync");
    }

    [Fact]
    public void ParseManagementQueues_OurFaultQueueWithZeroMessages_IsNotACandidate()
    {
        const string payload = """[{"name":"appraisal-sync_error","messages":0}]""";
        var ourFaultQueues = new HashSet<string>(StringComparer.Ordinal) { "appraisal-sync_error" };

        var (faultQueues, _) = FailedMessageCollectorService.ParseManagementQueues(payload, ourFaultQueues);

        faultQueues.Should().BeEmpty();
    }

    /// <summary>
    /// A JSON null or a missing field anywhere in a queue's snapshot data is treated as absent — never
    /// a reason to throw and reject the WHOLE payload. Sprinkles nulls into every nullable spot: a null
    /// numeric field, a null message_stats object entirely, and a null entry inside the samples array.
    /// </summary>
    [Fact]
    public void ParseManagementQueues_NullsSprinkledThroughout_DoesNotThrow_TreatsThemAsAbsent()
    {
        const string payload = """
            [
              {
                "name": "appraisal-sync",
                "messages_ready": null,
                "messages_unacknowledged": 3,
                "consumers": null,
                "message_stats": null,
                "messages_ready_details": {
                  "samples": [ {"sample": 1}, null, {"sample": 3} ]
                }
              },
              {
                "name": "workflow-instance-variables",
                "messages_unacknowledged": null
              }
            ]
            """;

        var ourFaultQueues = new HashSet<string>(StringComparer.Ordinal)
        {
            "appraisal-sync_error", "appraisal-sync_skipped",
            "workflow-instance-variables_error", "workflow-instance-variables_skipped"
        };
        var act = () => FailedMessageCollectorService.ParseManagementQueues(payload, ourFaultQueues);

        act.Should().NotThrow();

        var (_, queuesJson) = act();
        using var doc = JsonDocument.Parse(queuesJson);
        var queues = doc.RootElement.EnumerateArray().ToList();

        var appraisalSync = queues.Single(q => q.GetProperty("name").GetString() == "appraisal-sync");
        Assert.Equal(JsonValueKind.Null, appraisalSync.GetProperty("ready").ValueKind);
        Assert.Equal(3, appraisalSync.GetProperty("unacked").GetInt64());
        Assert.Equal(JsonValueKind.Null, appraisalSync.GetProperty("consumers").ValueKind);
        var samples = appraisalSync.GetProperty("samples").EnumerateArray().Select(s => s.GetInt32()).ToList();
        samples.Should().Equal(3, 1); // oldest→newest after reversing; the null entry is skipped entirely

        var workflow = queues.Single(q => q.GetProperty("name").GetString() == "workflow-instance-variables");
        Assert.Equal(JsonValueKind.Null, workflow.GetProperty("ready").ValueKind);
        Assert.Equal(JsonValueKind.Null, workflow.GetProperty("unacked").ValueKind);
    }

    [Fact]
    public void ParseManagementQueues_QueueEntryMissingName_IsSkipped_NotWholePayloadRejected()
    {
        const string payload = """
            [
              {"messages": 1},
              {"name": "appraisal-sync", "messages": 0}
            ]
            """;

        var act = () => FailedMessageCollectorService.ParseManagementQueues(payload, new HashSet<string>());

        act.Should().NotThrow();
    }
}
