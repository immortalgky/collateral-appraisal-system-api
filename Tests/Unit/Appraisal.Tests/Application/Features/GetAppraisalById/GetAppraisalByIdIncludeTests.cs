using System.Data;
using System.Data.Common;
using System.Text.Json;
using Appraisal.Application.Features.Appraisals.GetAppraisalById;
using Appraisal.Application.Features.Appraisals.GetAppraisalRequest;
using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Mapster;
using MediatR;
using NSubstitute;
using Request.Contracts.Requests.Dtos;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Tests.Application.Features.GetAppraisalById;

/// <summary>
/// GET /appraisals/{id}: the default read is the header alone; ?include=request / documents add their part and
/// nothing else runs; the files carry the original code plus the suggested one; a credit-side caller gets no
/// download ids until the appraisal is released.
/// </summary>
public class GetAppraisalByIdIncludeTests
{
    private static readonly Guid AppraisalId = Guid.NewGuid();
    private static readonly Guid SummaryFile = Guid.NewGuid();
    private static readonly Guid RequestFile = Guid.NewGuid();
    private static readonly Guid TitleFile = Guid.NewGuid();

    private static (GetAppraisalByIdQueryHandler Handler, ISender Sender, HeaderConnection Connection) Build(
        string status, bool fullViewer = true, bool trackingOnly = false, Guid? companyId = null,
        bool assignedToCaller = true, bool partsMissing = false)
    {
        var connection = new HeaderConnection(status, assignedToCaller);
        var factory = Substitute.For<ISqlConnectionFactory>();
        factory.GetOpenConnection().Returns(connection);

        var user = Substitute.For<ICurrentUserService>();
        user.HasPermission("APPRAISAL_VIEW").Returns(fullViewer);
        user.HasPermission("APPRAISAL_TRACKING_VIEW").Returns(trackingOnly);
        user.CompanyId.Returns(companyId);

        var sender = Substitute.For<ISender>();
        if (partsMissing)
        {
            // The request is soft-deleted / has no detail: both reads say "appraisal not found".
            sender.Send(Arg.Any<GetAppraisalRequestQuery>(), Arg.Any<CancellationToken>())
                .Returns<AppraisalRequestDto>(_ => throw new AppraisalNotFoundException(AppraisalId));
            sender.Send(Arg.Any<GetCarryForwardDocumentsQuery>(), Arg.Any<CancellationToken>())
                .Returns<CarryForwardDocumentsResult>(_ => throw new AppraisalNotFoundException(AppraisalId));
        }
        else
        {
            sender.Send(Arg.Any<GetAppraisalRequestQuery>(), Arg.Any<CancellationToken>()).Returns(new AppraisalRequestDto(
                new PrevAppraisalDto(Guid.NewGuid(), "69000000", 1m, null),
                new RequestDetailCopyDto(false, null, null, null), [], [], []));
            sender.Send(Arg.Any<GetCarryForwardDocumentsQuery>(), Arg.Any<CancellationToken>()).Returns(new CarryForwardDocumentsResult(
                AppraisalId, "69000001",
                [
                    Doc(RequestFile, "D001", "D001", "Request", null),
                    Doc(TitleFile, "D005", "D005", "Title", "01"),
                    Doc(SummaryFile, "D036", "D043", "Request", null)
                ]));
        }

        return (new GetAppraisalByIdQueryHandler(factory, user, sender), sender, connection);
    }

    private static CarryForwardDocumentDto Doc(Guid id, string retyped, string source, string level, string? collateralType) =>
        new(id, retyped, source, level, level == "Title" ? Guid.NewGuid() : null, collateralType,
            collateralType is null ? null : "A-1", "a.pdf", "/nas/a.pdf", null, 1, null, "u", "U", DateTime.Now, true);

    [Fact]
    public async Task The_default_read_is_the_header_alone_runs_no_other_query_and_has_no_request_or_documents_in_the_json()
    {
        var (handler, sender, _) = Build("Completed");

        var result = await handler.Handle(new GetAppraisalByIdQuery(AppraisalId), TestContext.Current.CancellationToken);

        Assert.Equal("Completed", result.Status);
        Assert.Null(result.Request);
        Assert.Null(result.Documents);
        Assert.Empty(sender.ReceivedCalls());

        var json = JsonSerializer.Serialize(result.Adapt<GetAppraisalByIdResponse>());
        Assert.DoesNotContain("\"Request\"", json);
        Assert.DoesNotContain("\"Documents\"", json);
    }

    [Fact]
    public async Task Include_request_adds_the_request_data_and_does_not_read_the_files()
    {
        var (handler, sender, _) = Build("Completed");

        var result = await handler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, AppraisalInclude.Request), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Request);
        Assert.Null(result.Documents);
        await sender.DidNotReceive().Send(Arg.Any<GetCarryForwardDocumentsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Include_documents_gives_the_original_code_and_the_suggested_one_for_any_status()
    {
        var (handler, sender, _) = Build("InProgress");

        var result = await handler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, AppraisalInclude.Documents), TestContext.Current.CancellationToken);

        Assert.Null(result.Request);
        var docs = result.Documents!;
        Assert.Equal(3, docs.Count);
        Assert.Equal(("D001", "D001"), (docs[0].DocumentType, docs[0].SuggestedType));
        Assert.Equal(("D043", "D036"), (docs[2].DocumentType, docs[2].SuggestedType)); // the summary report
        Assert.Equal("Title", docs[1].Level);
        Assert.Equal("01", docs[1].CollateralType);
        Assert.NotNull(docs[1].TitleId);
        // Not Completed is not a 409 here: the data is returned and the consumer reads the status from the header.
        await sender.Received(1).Send(
            Arg.Is<GetCarryForwardDocumentsQuery>(q => q.AnyStatus && q.AppraisalId == AppraisalId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Both_includes_add_both_parts()
    {
        var (handler, _, _) = Build("Completed");

        var result = await handler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, AppraisalInclude.Request | AppraisalInclude.Documents),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result.Request);
        Assert.Equal(3, result.Documents!.Count);
    }

    // (status, holds APPRAISAL_VIEW, holds only the tracking permission) -> are the id and path released?
    [Theory]
    [InlineData("InProgress", false, true, false)]   // credit-side caller, not released: withheld
    [InlineData("Completed", false, true, true)]     // released
    [InlineData("InProgress", true, false, true)]    // a full appraisal viewer always gets them
    [InlineData("InProgress", false, false, false)]  // NO permission is not "tracking only", yet is withheld too
    [InlineData("Completed", false, false, true)]    // a request maker picking a Completed prior gets everything
    public async Task The_download_ids_and_paths_follow_the_release_rule(
        string status, bool fullViewer, bool trackingOnly, bool released)
    {
        var (handler, _, _) = Build(status, fullViewer, trackingOnly);

        var result = await handler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, AppraisalInclude.Request | AppraisalInclude.Documents),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Documents!.Count); // the rows themselves are still listed
        Assert.All(result.Documents, d =>
        {
            Assert.Equal(released, d.DocumentId is not null);
            Assert.Equal(released, d.FilePath is not null); // the share is served as static files: a path is a link
        });
        // The request part is the same for everyone: its prior book is a past appraisal's, nothing unreleased.
        Assert.Equal(1m, result.Request!.PrevAppraisal!.AppraisalValue);
    }

    // The header value: the credit-side audience is always masked until released; a caller with NO appraisal
    // permission (an Integration client token) is too when an include or the Integration route (StrictRelease) is used,
    // but not on the plain Appraisal route, where RequestMaker / RequestChecker legitimately read it.
    [Theory]
    [InlineData("InProgress", false, false, AppraisalInclude.None, false, true)]    // plain route, no permission: unchanged
    [InlineData("InProgress", false, false, AppraisalInclude.Documents, false, false)] // an include: withheld
    [InlineData("InProgress", false, false, AppraisalInclude.None, true, false)]    // Integration route, no include: withheld
    [InlineData("Completed", false, false, AppraisalInclude.None, true, true)]      // released: kept
    [InlineData("InProgress", true, false, AppraisalInclude.Request, true, true)]   // APPRAISAL_VIEW: kept
    [InlineData("InProgress", false, true, AppraisalInclude.None, false, false)]    // credit-side: always withheld
    public async Task The_header_value_follows_the_release_rule(
        string status, bool fullViewer, bool trackingOnly, AppraisalInclude include, bool strict, bool valueExpected)
    {
        var (handler, _, _) = Build(status, fullViewer, trackingOnly);

        var result = await handler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, include, strict), TestContext.Current.CancellationToken);

        Assert.Equal(valueExpected ? 100m : null, result.AppraisalValue);
    }

    [Fact]
    public async Task A_request_or_documents_part_that_cannot_be_read_leaves_the_header_a_200()
    {
        var (handler, _, _) = Build("Completed", partsMissing: true);

        var result = await handler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, AppraisalInclude.Request | AppraisalInclude.Documents),
            TestContext.Current.CancellationToken);

        Assert.Equal("Completed", result.Status);
        Assert.Null(result.Request);
        Assert.Empty(result.Documents!);
    }

    [Fact]
    public async Task An_external_caller_outside_the_appraisals_company_gets_404_on_an_include()
    {
        var (handler, sender, connection) = Build("Completed", companyId: Guid.NewGuid(), assignedToCaller: false);

        await Assert.ThrowsAsync<AppraisalNotFoundException>(() => handler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, AppraisalInclude.Documents), TestContext.Current.CancellationToken));

        Assert.Equal(1, connection.ScopeChecks);
        Assert.Empty(sender.ReceivedCalls()); // nothing about the request or files was read
    }

    [Fact]
    public async Task An_external_caller_of_the_assigned_company_reads_the_includes()
    {
        var (handler, _, connection) = Build("Completed", companyId: Guid.NewGuid(), assignedToCaller: true);

        var result = await handler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, AppraisalInclude.Documents), TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Documents!.Count);
        Assert.Equal(1, connection.ScopeChecks);
    }

    [Fact]
    public async Task An_internal_caller_is_not_scoped_and_the_default_read_is_never_scoped()
    {
        var (internalHandler, _, internalConnection) = Build("Completed", companyId: null, assignedToCaller: false);
        await internalHandler.Handle(
            new GetAppraisalByIdQuery(AppraisalId, AppraisalInclude.Documents), TestContext.Current.CancellationToken);
        Assert.Equal(0, internalConnection.ScopeChecks);

        // The header itself carries no company scope (documented in its handler); only the includes do.
        var (externalHandler, _, externalConnection) = Build("Completed", companyId: Guid.NewGuid(), assignedToCaller: false);
        var header = await externalHandler.Handle(new GetAppraisalByIdQuery(AppraisalId), TestContext.Current.CancellationToken);
        Assert.Equal("Completed", header.Status);
        Assert.Equal(0, externalConnection.ScopeChecks);
    }

    // Dapper needs a DbConnection for its async calls, so NSubstitute's IDbConnection will not do.
    private sealed class HeaderConnection(string status, bool assigned) : DbConnection
    {
        public string Status => status;
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
        protected override DbCommand CreateDbCommand() => new HeaderCommand(this);
    }

    private sealed class HeaderCommand(HeaderConnection owner) : DbCommand
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
        public override object? ExecuteScalar()
        {
            owner.ScopeChecks++;
            return owner.Assigned;
        }
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => Substitute.For<DbParameter>();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(Guid));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("AppraisalType", typeof(string));
            table.Columns.Add("Priority", typeof(string));
            table.Columns.Add("AppraisalValue", typeof(decimal));
            table.Rows.Add(AppraisalId, owner.Status, "New", "Normal", 100m);
            return table.CreateDataReader();
        }
    }
}
