using System.Data;
using System.Data.Common;
using Appraisal.Application.Features.Appraisals.GetCarryForwardDocuments;
using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using NSubstitute;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Tests.Application.Features.GetCarryForwardDocuments;

/// <summary>An external (company) caller reads only appraisals assigned to its company; otherwise it is "not found".</summary>
public class GetCarryForwardDocumentsScopeTests
{
    private static readonly Guid AppraisalId = Guid.NewGuid();

    private static (GetCarryForwardDocumentsQueryHandler Handler, FakeConnection Connection) Handler(
        Guid? callerCompanyId, bool assignedToCaller)
    {
        var connection = new FakeConnection(assignedToCaller);
        var factory = Substitute.For<ISqlConnectionFactory>();
        factory.GetOpenConnection().Returns(connection);
        var user = Substitute.For<ICurrentUserService>();
        user.CompanyId.Returns(callerCompanyId);
        return (new GetCarryForwardDocumentsQueryHandler(factory, user), connection);
    }

    [Fact]
    public async Task An_external_caller_is_refused_an_appraisal_outside_its_company_as_not_found()
    {
        var (handler, connection) = Handler(Guid.NewGuid(), assignedToCaller: false);

        await Assert.ThrowsAsync<AppraisalNotFoundException>(() =>
            handler.Handle(new GetCarryForwardDocumentsQuery(AppraisalId, EnforceCallerScope: true), TestContext.Current.CancellationToken));

        Assert.Equal(0, connection.HeaderReads); // nothing about the appraisal was read
    }

    [Fact]
    public async Task An_external_caller_reads_an_appraisal_assigned_to_its_company()
    {
        var (handler, _) = Handler(Guid.NewGuid(), assignedToCaller: true);

        var result = await handler.Handle(
            new GetCarryForwardDocumentsQuery(AppraisalId, EnforceCallerScope: true), TestContext.Current.CancellationToken);

        Assert.Equal(AppraisalId, result.AppraisalId);
    }

    [Fact]
    public async Task Without_the_flag_nobody_is_scoped_the_identity_is_not_even_read()
    {
        // Integration (LOS) and background callers send the query as it is: no company user to scope by.
        var (handler, connection) = Handler(Guid.NewGuid(), assignedToCaller: false);

        var result = await handler.Handle(
            new GetCarryForwardDocumentsQuery(AppraisalId), TestContext.Current.CancellationToken);

        Assert.Equal(AppraisalId, result.AppraisalId);
        Assert.Equal(0, connection.ScopeChecks);
    }

    [Fact]
    public async Task An_internal_caller_is_not_scoped_and_the_scope_query_is_never_run()
    {
        var (handler, connection) = Handler(null, assignedToCaller: false);

        var result = await handler.Handle(
            new GetCarryForwardDocumentsQuery(AppraisalId, EnforceCallerScope: true), TestContext.Current.CancellationToken);

        Assert.Equal(AppraisalId, result.AppraisalId);
        Assert.Equal(0, connection.ScopeChecks);
    }

    // Dapper needs a DbConnection for its async calls, so NSubstitute's IDbConnection will not do.
    private sealed class FakeConnection(bool assignedToCaller) : DbConnection
    {
        public int ScopeChecks;
        public int HeaderReads;
        public bool AssignedToCaller => assignedToCaller;

        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new FakeCommand(this);
    }

    private sealed class FakeCommand(FakeConnection owner) : DbCommand
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
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => Substitute.For<DbParameter>();

        public override object? ExecuteScalar()
        {
            owner.ScopeChecks++;
            return owner.AssignedToCaller;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var table = new DataTable();
            if (CommandText.Contains("AppraisalType", StringComparison.Ordinal))
            {
                owner.HeaderReads++;
                table.Columns.Add("AppraisalId", typeof(Guid));
                table.Columns.Add("AppraisalNumber", typeof(string));
                table.Columns.Add("Status", typeof(string));
                table.Columns.Add("AppraisalType", typeof(string));
                table.Columns.Add("ProjectExists", typeof(bool));
                table.Rows.Add(AppraisalId, "69000001", "Completed", "New", false);
            }
            else
            {
                foreach (var (name, type) in new (string, Type)[]
                         {
                             ("Level", typeof(string)), ("DocumentId", typeof(Guid)), ("DocumentType", typeof(string)),
                             ("PriorTitleId", typeof(Guid)), ("CollateralType", typeof(string)), ("TitleNumber", typeof(string)),
                             ("FileName", typeof(string)), ("FilePath", typeof(string)), ("Prefix", typeof(string)),
                             ("Set", typeof(int)), ("Notes", typeof(string)), ("UploadedBy", typeof(string)),
                             ("UploadedByName", typeof(string)), ("UploadedAt", typeof(DateTime)),
                             ("CarryForwardByDefault", typeof(bool))
                         })
                    table.Columns.Add(name, type);
            }

            return table.CreateDataReader();
        }
    }
}
