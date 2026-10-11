using System.Data;
using System.Data.Common;
using Appraisal.Application.Features.Appraisals.GetAppraisalRequest;
using NSubstitute;
using Shared.Data;

namespace Appraisal.Tests.Application.Features.GetAppraisalById;

/// <summary>
/// request.prevAppraisal is THIS appraisal's own prior book (the prior its request referenced), not a snapshot of the
/// appraisal being read.
/// </summary>
public class GetAppraisalRequestQueryHandlerTests
{
    private static readonly Guid AppraisalId = Guid.NewGuid();      // "book 2", the appraisal being read
    private static readonly Guid PriorBookId = Guid.NewGuid();      // "book 1", the prior its request referenced

    private static GetAppraisalRequestQueryHandler Build(
        Guid? prevId, string? prevNumber, decimal? prevValue, DateTime? prevDate)
    {
        var factory = Substitute.For<ISqlConnectionFactory>();
        factory.GetOpenConnection().Returns(new RequestConnection(prevId, prevNumber, prevValue, prevDate));
        return new GetAppraisalRequestQueryHandler(factory);
    }

    private static Task<AppraisalRequestDto> Read(GetAppraisalRequestQueryHandler handler) =>
        handler.Handle(new GetAppraisalRequestQuery(AppraisalId), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Book_2_describes_book_1_as_its_prior_not_itself()
    {
        var date = new DateTime(2026, 4, 22, 14, 13, 45);

        var request = await Read(Build(PriorBookId, "69000001", 1500000m, date));

        var prior = request.PrevAppraisal!;
        Assert.Equal(PriorBookId, prior.AppraisalId);
        Assert.Equal("69000001", prior.AppraisalNumber);
        Assert.Equal(1500000m, prior.AppraisalValue);
        Assert.Equal(date, prior.AppraisalDate);
        Assert.NotEqual(AppraisalId, prior.AppraisalId);
    }

    [Fact]
    public async Task A_request_with_no_prior_has_a_null_prevAppraisal() =>
        Assert.Null((await Read(Build(null, null, null, null))).PrevAppraisal);

    [Fact]
    public async Task A_blank_number_with_no_id_is_no_prior() =>
        Assert.Null((await Read(Build(null, "  ", null, null))).PrevAppraisal);

    [Fact]
    public async Task A_legacy_99A_prior_has_its_number_only()
    {
        var request = await Read(Build(null, "99A0001234", null, null));

        var prior = request.PrevAppraisal!;
        Assert.Null(prior.AppraisalId);
        Assert.Equal("99A0001234", prior.AppraisalNumber);
        Assert.Null(prior.AppraisalValue);
        Assert.Null(prior.AppraisalDate);
    }

    // Dapper needs a DbConnection for its async calls, so NSubstitute's IDbConnection will not do.
    private sealed class RequestConnection(Guid? prevId, string? prevNumber, decimal? prevValue, DateTime? prevDate) : DbConnection
    {
        public (Guid? Id, string? Number, decimal? Value, DateTime? Date) Prior => (prevId, prevNumber, prevValue, prevDate);
        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new RequestCommand(this);
    }

    private sealed class RequestCommand(RequestConnection owner) : DbCommand
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
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => Substitute.For<DbParameter>();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var table = new DataTable();
            if (CommandText.Contains("vw_AppraisalCopyTemplate", StringComparison.Ordinal))
            {
                table.Columns.Add("AppraisalId", typeof(Guid));
                table.Columns.Add("RequestId", typeof(Guid));
                table.Columns.Add("PrevAppraisalId", typeof(Guid));
                table.Columns.Add("PrevAppraisalNumber", typeof(string));
                table.Columns.Add("PrevAppraisalValue", typeof(decimal));
                table.Columns.Add("PrevAppraisalDate", typeof(DateTime));
                var (id, number, value, date) = owner.Prior;
                table.Rows.Add(
                    AppraisalId, Guid.NewGuid(),
                    (object?)id ?? DBNull.Value, (object?)number ?? DBNull.Value,
                    (object?)value ?? DBNull.Value, (object?)date ?? DBNull.Value);
            }
            // customers, properties, titles: no rows
            return table.CreateDataReader();
        }
    }
}
