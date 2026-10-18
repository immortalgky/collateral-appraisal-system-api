using System.Data;
using System.Data.Common;
using Appraisal.Application.Features.Appraisals.GetNextInspectionNumber;
using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using MediatR;
using NSubstitute;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Tests.Application.Features.GetAppraisalById;

/// <summary>The preview of the round a new CI request copying this appraisal would be: the chain count + 1, as stamped at creation.</summary>
public class GetNextInspectionNumberQueryHandlerTests
{
    private static readonly Guid AppraisalId = Guid.NewGuid();

    private static (GetNextInspectionNumberQueryHandler Handler, ISender Sender, ScopeConnection Connection) Build(
        int? progressiveCount, Guid? companyId = null, bool assignedToCaller = true)
    {
        var connection = new ScopeConnection(assignedToCaller);
        var factory = Substitute.For<ISqlConnectionFactory>();
        factory.GetOpenConnection().Returns(connection);
        var user = Substitute.For<ICurrentUserService>();
        user.CompanyId.Returns(companyId);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ResolveLatestInAppraisalChainQuery>(), Arg.Any<CancellationToken>())
            .Returns(progressiveCount is { } n ? new AppraisalChainRef(AppraisalId, null, "", n) : null);
        return (new GetNextInspectionNumberQueryHandler(factory, user, sender), sender, connection);
    }

    private static Task<GetNextInspectionNumberResult> Read(GetNextInspectionNumberQueryHandler handler) =>
        handler.Handle(new GetNextInspectionNumberQuery(AppraisalId), TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    public async Task The_next_round_is_the_chain_count_plus_one(int inChain, int expected)
    {
        var (handler, sender, _) = Build(inChain);

        Assert.Equal(expected, (await Read(handler)).NextInspectionNumber);
        await sender.Received(1).Send(
            Arg.Is<ResolveLatestInAppraisalChainQuery>(q => q.PickedPrevAppraisalId == AppraisalId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_appraisal_with_no_chain_is_not_found() =>
        await Assert.ThrowsAsync<AppraisalNotFoundException>(() => Read(Build(null).Handler));

    [Fact]
    public async Task An_external_caller_outside_the_appraisals_company_gets_404_and_the_chain_is_not_read()
    {
        var (handler, sender, connection) = Build(2, companyId: Guid.NewGuid(), assignedToCaller: false);

        await Assert.ThrowsAsync<AppraisalNotFoundException>(() => Read(handler));

        Assert.Equal(1, connection.ScopeChecks);
        Assert.Empty(sender.ReceivedCalls());
    }

    [Fact]
    public async Task An_external_caller_of_the_assigned_company_and_an_internal_caller_read_it()
    {
        var (external, _, externalConnection) = Build(2, companyId: Guid.NewGuid(), assignedToCaller: true);
        Assert.Equal(3, (await Read(external)).NextInspectionNumber);
        Assert.Equal(1, externalConnection.ScopeChecks);

        var (internalHandler, _, internalConnection) = Build(2);
        Assert.Equal(3, (await Read(internalHandler)).NextInspectionNumber);
        Assert.Equal(0, internalConnection.ScopeChecks);
    }

    // Dapper needs a DbConnection for its async calls, so NSubstitute's IDbConnection will not do.
    private sealed class ScopeConnection(bool assigned) : DbConnection
    {
        public bool Assigned => assigned;
        public int ScopeChecks;
        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new ScopeCommand(this);
    }

    private sealed class ScopeCommand(ScopeConnection owner) : DbCommand
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
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        public override object? ExecuteScalar()
        {
            owner.ScopeChecks++;
            return owner.Assigned;
        }
    }
}
