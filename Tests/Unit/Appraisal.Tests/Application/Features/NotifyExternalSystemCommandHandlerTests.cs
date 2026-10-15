using System.Data;
using System.Data.Common;
using System.Text.Json;
using Appraisal.Application.Features.Appraisals.NotifyExternalSystem;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Handler-level rules for <see cref="NotifyExternalSystemCommandHandler"/>: Completed-only, an external
/// source is required, and the event plus the audit row describe the same notification. Like the document
/// correction handler, the audit row is only added to the context; TransactionalBehavior saves it together
/// with the outboxed event.
/// </summary>
public class NotifyExternalSystemCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 10, 0, 0);
    private static readonly DateTime ApprovedAt = new(2026, 9, 1, 15, 30, 0);

    private readonly IAppraisalRepository _appraisals = Substitute.For<IAppraisalRepository>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public NotifyExternalSystemCommandHandlerTests()
    {
        _currentUser.UserCode.Returns("EMP042");
        _clock.ApplicationNow.Returns(Now);
    }

    private NotifyExternalSystemCommandHandler CreateHandler(
        string? externalSystem = "LOS", string? externalCaseKey = "CASE-1", bool summaryAttached = true,
        string? appraisalNumber = "AP-2569-00042", bool hasSubscription = true)
    {
        var connectionFactory = Substitute.For<ISqlConnectionFactory>();
        connectionFactory.GetOpenConnection().Returns(
            new FakeConnection(externalSystem, externalCaseKey, summaryAttached, appraisalNumber, hasSubscription));
        return new(_appraisals, _db, connectionFactory, _outbox, _currentUser, _clock);
    }

    private AppraisalAggregate GivenAppraisal(bool completed = true)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", new DateTime(2026, 1, 1));
        if (completed)
        {
            appraisal.SyncStatusFromWorkflow(AppraisalStatus.Completed);
            appraisal.MarkApprovedByCommittee("COM01", ApprovedAt);
        }
        _appraisals.GetByIdAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);
        return appraisal;
    }

    [Fact]
    public async Task Happy_path_publishes_the_event_and_writes_the_audit_row()
    {
        var appraisal = GivenAppraisal();

        var result = await CreateHandler().Handle(
            new NotifyExternalSystemCommand(appraisal.Id, "  LOS missed it  "), TestContext.Current.CancellationToken);

        Assert.Equal("LOS", result.ExternalSystem);
        _outbox.Received(1).Publish(
            Arg.Is<AppraisalResultReadyIntegrationEvent>(e =>
                e.AppraisalId == appraisal.Id
                && e.RequestId == appraisal.RequestId
                && e.CompletedAt == ApprovedAt
                && e.DocumentReady
                && e.FailureReason == null),
            correlationId: appraisal.Id.ToString());

        var log = Assert.Single(_db.AppraisalPropertyCorrectionLogs.Local);
        Assert.Null(log.AppraisalPropertyId);
        Assert.Equal("DOCUMENT", log.PropertyType);
        Assert.Equal("LOS missed it", log.Reason);
        Assert.Equal("EMP042", log.ChangedBy);
        Assert.Equal(Now, log.ChangedAt);
        var entry = JsonDocument.Parse(log.ChangedFields).RootElement.GetProperty("ExternalNotification");
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("from").ValueKind);
        Assert.Equal("LOS", entry.GetProperty("to").GetString());
    }

    [Fact]
    public async Task Without_an_attached_summary_the_event_says_so()
    {
        var appraisal = GivenAppraisal();

        await CreateHandler(summaryAttached: false).Handle(
            new NotifyExternalSystemCommand(appraisal.Id, "reason"), TestContext.Current.CancellationToken);

        _outbox.Received(1).Publish(
            Arg.Is<AppraisalResultReadyIntegrationEvent>(e =>
                !e.DocumentReady && e.FailureReason == "No appraisal summary attached"),
            correlationId: appraisal.Id.ToString());
    }

    [Fact]
    public async Task Appraisal_that_is_not_Completed_is_refused_and_nothing_is_written()
    {
        var appraisal = GivenAppraisal(completed: false);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().Handle(
            new NotifyExternalSystemCommand(appraisal.Id, "reason"), TestContext.Current.CancellationToken));

        Assert.Equal("APPRAISAL_NOT_COMPLETED", exception.Code);
        _outbox.DidNotReceiveWithAnyArgs().Publish(Arg.Any<AppraisalResultReadyIntegrationEvent>());
        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    [Theory]
    [InlineData(null, "CASE-1")]
    [InlineData("LOS", null)]
    [InlineData("", "")]
    public async Task Appraisal_without_an_external_source_is_refused_and_nothing_is_written(
        string? externalSystem, string? externalCaseKey)
    {
        var appraisal = GivenAppraisal();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler(externalSystem, externalCaseKey)
            .Handle(new NotifyExternalSystemCommand(appraisal.Id, "reason"), TestContext.Current.CancellationToken));

        Assert.Equal("NO_EXTERNAL_SOURCE", exception.Code);
        _outbox.DidNotReceiveWithAnyArgs().Publish(Arg.Any<AppraisalResultReadyIntegrationEvent>());
        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    [Fact]
    public async Task Appraisal_without_a_number_has_no_external_source()
    {
        // The webhook consumer skips when AppraisalNumber is empty, so notifying would be a silent no-op.
        var appraisal = GivenAppraisal();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler(appraisalNumber: null)
            .Handle(new NotifyExternalSystemCommand(appraisal.Id, "reason"), TestContext.Current.CancellationToken));

        Assert.Equal("NO_EXTERNAL_SOURCE", exception.Code);
        _outbox.DidNotReceiveWithAnyArgs().Publish(Arg.Any<AppraisalResultReadyIntegrationEvent>());
    }

    [Fact]
    public async Task System_without_an_active_subscription_has_no_external_source()
    {
        // WebhookService drops the send without a subscription; recording "notified" would be false history.
        var appraisal = GivenAppraisal();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler(hasSubscription: false)
            .Handle(new NotifyExternalSystemCommand(appraisal.Id, "reason"), TestContext.Current.CancellationToken));

        Assert.Equal("NO_EXTERNAL_SOURCE", exception.Code);
        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    // ── validator: the blank-reason rule never reaches the handler ──

    private static readonly NotifyExternalSystemCommandValidator Validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_reason_is_invalid(string reason) =>
        Assert.False(Validator.Validate(new NotifyExternalSystemCommand(Guid.NewGuid(), reason)).IsValid);

    [Fact]
    public void Reason_over_4000_characters_is_invalid() =>
        Assert.False(Validator.Validate(new NotifyExternalSystemCommand(Guid.NewGuid(), new string('x', 4001))).IsValid);

    [Fact]
    public void A_reason_is_valid() =>
        Assert.True(Validator.Validate(new NotifyExternalSystemCommand(Guid.NewGuid(), "reason")).IsValid);

    // ── Dapper needs a DbConnection for its async calls, so NSubstitute's IDbConnection will not do ──

    private sealed class FakeConnection(
        string? externalSystem, string? externalCaseKey, bool summaryAttached, string? appraisalNumber,
        bool hasSubscription)
        : DbConnection
    {
        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new FakeCommand(
            externalSystem, externalCaseKey, summaryAttached, appraisalNumber, hasSubscription);
    }

    private sealed class FakeCommand(
        string? externalSystem, string? externalCaseKey, bool summaryAttached, string? appraisalNumber,
        bool hasSubscription) : DbCommand
    {
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = Substitute.For<DbParameterCollection>();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => summaryAttached;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => Substitute.For<DbParameter>();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            // Only the external-source lookup reads rows (the summary check is a scalar).
            var table = new DataTable();
            table.Columns.Add("AppraisalNumber", typeof(string));
            table.Columns.Add("ExternalCaseKey", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("CompletedAt", typeof(DateTime));
            table.Columns.Add("HasSubscription", typeof(bool));
            table.Rows.Add(appraisalNumber, externalCaseKey, externalSystem, ApprovedAt, hasSubscription);
            return table.CreateDataReader();
        }
    }
}
