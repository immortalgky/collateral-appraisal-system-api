using System.Data;
using System.Data.Common;
using Appraisal.Application.Services;
using Appraisal.Infrastructure;
using Document.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Reporting.Contracts;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Tests.Application.Services;

/// <summary>
/// The data-correction page's "regenerate without notifying the source system" must publish nothing, on any
/// path. Exercised on the appraisal-not-found path, where publishing is otherwise the job's whole work.
/// </summary>
public class AppraisalSummaryAutoAttachJobNotifyTests
{
    [Fact]
    public async Task NotifyExternal_false_publishes_nothing_and_fails_when_nothing_was_attached()
    {
        var connectionFactory = Substitute.For<ISqlConnectionFactory>();
        connectionFactory.CreateNewConnection().Returns(_ => new EmptyConnection());
        var outbox = Substitute.For<IIntegrationEventOutbox>();
        var db = new AppraisalDbContext(
            new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var job = new AppraisalSummaryAutoAttachJob(
            connectionFactory,
            Substitute.For<IReportPdfGenerator>(),
            Substitute.For<IDocumentCreator>(),
            Substitute.For<ISender>(),
            outbox,
            Substitute.For<IOutboxScope>(),
            db,
            Substitute.For<IDateTimeProvider>(),
            NullLogger<AppraisalSummaryAutoAttachJob>.Instance);

        // Nothing was attached, so the silent run must fail the job rather than report success.
        await Assert.ThrowsAsync<InvalidOperationException>(() => job.RunAsync(
            Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 9, 1), force: true, notifyExternal: false,
            TestContext.Current.CancellationToken));

        outbox.DidNotReceiveWithAnyArgs().Publish(Arg.Any<AppraisalResultReadyIntegrationEvent>());
    }

    // ── Dapper needs a DbConnection; this one answers every query with no rows (appraisal not found) ──

    private sealed class EmptyConnection : DbConnection
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
        protected override DbCommand CreateDbCommand() => new EmptyCommand();
    }

    private sealed class EmptyCommand : DbCommand
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
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => Substitute.For<DbParameter>();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new DataTable().CreateDataReader();
    }
}
