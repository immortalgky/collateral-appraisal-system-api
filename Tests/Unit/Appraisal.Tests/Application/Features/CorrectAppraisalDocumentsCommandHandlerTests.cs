using System.Data;
using System.Data.Common;
using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CorrectAppraisalDocuments;
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
/// Handler-level rules for <see cref="CorrectAppraisalDocumentsCommandHandler"/>: the Completed-only gate,
/// the document change itself, and the audit row that must describe it. The audit row is only added to the
/// context here — TransactionalBehavior saves it together with the document change, which is what makes
/// the two atomic and is not something a unit test can show.
/// </summary>
public class CorrectAppraisalDocumentsCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 10, 0, 0);

    private readonly IAppraisalRepository _appraisals = Substitute.For<IAppraisalRepository>();
    private readonly IAppraisalDocumentRepository _documents = Substitute.For<IAppraisalDocumentRepository>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public CorrectAppraisalDocumentsCommandHandlerTests()
    {
        _currentUser.UserCode.Returns("EMP042");
        _clock.ApplicationNow.Returns(Now);
    }

    /// <summary>
    /// The handler asks the database three things through Dapper: is the type valid (scalar), what did the
    /// upload store (row), then what sort order is next (scalar). The fake connection answers the scalars in
    /// order and the row from <paramref name="storedFileName"/> — null means the upload does not exist.
    /// </summary>
    private CorrectAppraisalDocumentsCommandHandler CreateHandler(
        bool typeIsValid = true, int? maxSortOrder = 2, string? storedFileName = "new.pdf",
        string storedMimeType = "application/pdf")
    {
        var connectionFactory = Substitute.For<ISqlConnectionFactory>();
        connectionFactory.GetOpenConnection().Returns(
            new FakeConnection(typeIsValid, maxSortOrder, storedFileName, storedMimeType));
        return new(_appraisals, _documents, _db, connectionFactory, _outbox, _currentUser, _clock);
    }

    private AppraisalAggregate GivenAppraisal(bool completed = true)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", new DateTime(2026, 1, 1));
        if (completed) appraisal.SyncStatusFromWorkflow(AppraisalStatus.Completed);
        _appraisals.GetByIdAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);
        return appraisal;
    }

    private AppraisalDocument GivenDocument(Guid appraisalId, string typeCode, string fileName, string? notes = null)
    {
        var document = AppraisalDocument.Create(
            appraisalId, typeCode, Guid.NewGuid(), fileName, "application/pdf", 10, notes, 0);
        _documents.GetByIdAndAppraisalIdAsync(document.Id, appraisalId, Arg.Any<CancellationToken>())
            .Returns(document);
        return document;
    }

    private static DocumentToAttach Attach(string typeCode) => new(typeCode, Guid.NewGuid());

    private AppraisalPropertyCorrectionLog SingleLog() =>
        Assert.Single(_db.AppraisalPropertyCorrectionLogs.Local);

    private static JsonElement ChangedFields(AppraisalPropertyCorrectionLog log) =>
        JsonDocument.Parse(log.ChangedFields).RootElement;

    private AppraisalDocument? _added;

    private void CaptureAddedDocument() =>
        _documents.AddAsync(Arg.Do<AppraisalDocument>(d => _added = d), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task Attach_adds_the_document_and_writes_a_from_null_audit_row()
    {
        CaptureAddedDocument();
        var appraisal = GivenAppraisal();
        var add = Attach("d005");
        var command = new CorrectAppraisalDocumentsCommand(appraisal.Id, "  missing file  ", null, add);

        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(_added);
        Assert.Equal(_added.Id, result.AddedId);
        Assert.Equal("D005", _added.DocumentTypeCode);
        Assert.Equal(3, _added.SortOrder);            // one past the current max of 2
        Assert.Equal("EMP042", _added.UploadedByName); // server-side user, never the request

        _outbox.Received(1).Publish(
            Arg.Is<DocumentLinkedIntegrationEventV2>(e => e.DocumentId == add.DocumentId),
            correlationId: appraisal.Id.ToString());

        var log = SingleLog();
        Assert.Null(log.AppraisalPropertyId);
        Assert.Equal("DOCUMENT", log.PropertyType);
        Assert.Equal("missing file", log.Reason);
        Assert.Equal("EMP042", log.ChangedBy);
        Assert.Equal(Now, log.ChangedAt);
        var d005 = ChangedFields(log).GetProperty("D005");
        Assert.Equal(JsonValueKind.Null, d005.GetProperty("from").ValueKind);
        Assert.Equal("new.pdf", d005.GetProperty("to").GetString());
    }

    [Fact]
    public async Task Delete_removes_the_document_and_writes_a_to_null_audit_row()
    {
        var appraisal = GivenAppraisal();
        var existing = GivenDocument(appraisal.Id, "D005", "old.pdf");
        var command = new CorrectAppraisalDocumentsCommand(appraisal.Id, "wrong file", existing.Id, null);

        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        Assert.Null(result.AddedId);
        await _documents.Received(1).DeleteAsync(existing, Arg.Any<CancellationToken>());
        _outbox.Received(1).Publish(
            Arg.Is<DocumentUnlinkedIntegrationEvent>(e => e.DocumentId == existing.DocumentId));

        var d005 = ChangedFields(SingleLog()).GetProperty("D005");
        Assert.Equal("old.pdf", d005.GetProperty("from").GetString());
        Assert.Equal(JsonValueKind.Null, d005.GetProperty("to").ValueKind);
    }

    [Fact]
    public async Task Replace_within_one_type_is_a_single_from_to_entry()
    {
        CaptureAddedDocument();
        var appraisal = GivenAppraisal();
        var existing = GivenDocument(appraisal.Id, "D005", "old.pdf", notes: "page 2 of the deed");
        var command = new CorrectAppraisalDocumentsCommand(
            appraisal.Id, "wrong file", existing.Id, Attach("D005"));

        await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        await _documents.Received(1).DeleteAsync(existing, Arg.Any<CancellationToken>());
        Assert.NotNull(_added);
        Assert.Equal(existing.SortOrder, _added.SortOrder); // takes the removed slot, not max + 1
        Assert.Equal("page 2 of the deed", _added.Notes);   // and its notes
        var fields = ChangedFields(SingleLog());
        Assert.Single(fields.EnumerateObject());
        Assert.Equal("old.pdf", fields.GetProperty("D005").GetProperty("from").GetString());
        Assert.Equal("new.pdf", fields.GetProperty("D005").GetProperty("to").GetString());
    }

    [Fact]
    public async Task Replace_across_two_types_writes_two_keys()
    {
        CaptureAddedDocument();
        var appraisal = GivenAppraisal();
        var existing = GivenDocument(appraisal.Id, "D005", "old.pdf");
        var command = new CorrectAppraisalDocumentsCommand(
            appraisal.Id, "wrong type", existing.Id, Attach("D006"));

        await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        var fields = ChangedFields(SingleLog());
        Assert.Equal(2, fields.EnumerateObject().Count());
        Assert.Equal("old.pdf", fields.GetProperty("D005").GetProperty("from").GetString());
        Assert.Equal(JsonValueKind.Null, fields.GetProperty("D005").GetProperty("to").ValueKind);
        Assert.Equal(JsonValueKind.Null, fields.GetProperty("D006").GetProperty("from").ValueKind);
        Assert.Equal("new.pdf", fields.GetProperty("D006").GetProperty("to").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Appraisal_that_is_not_Completed_is_refused_and_nothing_is_written(bool cancelled)
    {
        var appraisal = GivenAppraisal(completed: false);
        if (cancelled) appraisal.Cancel("EMP999", new DateTime(2026, 2, 1), "withdrawn");
        var command = new CorrectAppraisalDocumentsCommand(
            appraisal.Id, "reason", null, Attach("D005"));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateHandler().Handle(command, TestContext.Current.CancellationToken));

        Assert.Equal("APPRAISAL_NOT_COMPLETED", exception.Code);
        await _documents.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        _outbox.DidNotReceiveWithAnyArgs().Publish(Arg.Any<DocumentLinkedIntegrationEventV2>());
        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    [Fact]
    public async Task Unknown_appraisal_is_not_found()
    {
        _appraisals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((AppraisalAggregate?)null);
        var command = new CorrectAppraisalDocumentsCommand(Guid.NewGuid(), "reason", Guid.NewGuid(), null);

        await Assert.ThrowsAsync<Appraisal.Domain.Appraisals.Exceptions.AppraisalNotFoundException>(
            () => CreateHandler().Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Document_that_is_not_on_this_appraisal_is_not_found()
    {
        var appraisal = GivenAppraisal();
        // No GivenDocument: the repository finds nothing for (id, appraisalId).
        var command = new CorrectAppraisalDocumentsCommand(appraisal.Id, "reason", Guid.NewGuid(), null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateHandler().Handle(command, TestContext.Current.CancellationToken));

        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    [Fact]
    public async Task Invalid_document_type_is_a_bad_request()
    {
        var appraisal = GivenAppraisal();
        var command = new CorrectAppraisalDocumentsCommand(
            appraisal.Id, "reason", null, Attach("ZZZ"));

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateHandler(typeIsValid: false).Handle(command, TestContext.Current.CancellationToken));

        await _documents.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    [Fact]
    public async Task File_name_comes_from_the_stored_upload()
    {
        CaptureAddedDocument();
        var appraisal = GivenAppraisal();
        var command = new CorrectAppraisalDocumentsCommand(
            appraisal.Id, "reason", null, Attach("D005"));

        await CreateHandler(storedFileName: "scan-0042.pdf").Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal("scan-0042.pdf", _added!.FileName);
        Assert.Equal("scan-0042.pdf", ChangedFields(SingleLog()).GetProperty("D005").GetProperty("to").GetString());
    }

    [Fact]
    public async Task Upload_that_does_not_exist_is_not_found_and_nothing_is_written()
    {
        var appraisal = GivenAppraisal();
        var command = new CorrectAppraisalDocumentsCommand(
            appraisal.Id, "reason", null, Attach("D005"));

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateHandler(storedFileName: null).Handle(command, TestContext.Current.CancellationToken));

        await _documents.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    [Fact]
    public async Task Upload_that_is_not_an_image_or_pdf_is_a_bad_request()
    {
        var appraisal = GivenAppraisal();
        var command = new CorrectAppraisalDocumentsCommand(appraisal.Id, "reason", null, Attach("D005"));

        await Assert.ThrowsAsync<BadRequestException>(() => CreateHandler(
                storedMimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            .Handle(command, TestContext.Current.CancellationToken));

        await _documents.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Replacing_a_file_with_itself_is_a_bad_request()
    {
        var appraisal = GivenAppraisal();
        var existing = GivenDocument(appraisal.Id, "D005", "old.pdf");
        var command = new CorrectAppraisalDocumentsCommand(
            appraisal.Id, "reason", existing.Id, new DocumentToAttach("D005", existing.DocumentId));

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateHandler().Handle(command, TestContext.Current.CancellationToken));

        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    // ── validator: the "400 when neither" and "reason required" rules never reach the handler ──

    private static readonly CorrectAppraisalDocumentsCommandValidator Validator = new();

    [Fact]
    public void Neither_add_nor_remove_is_invalid()
    {
        var result = Validator.Validate(new CorrectAppraisalDocumentsCommand(Guid.NewGuid(), "reason", null, null));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_reason_is_invalid(string reason)
    {
        var result = Validator.Validate(
            new CorrectAppraisalDocumentsCommand(Guid.NewGuid(), reason, Guid.NewGuid(), null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Add_only_remove_only_and_both_are_valid()
    {
        var id = Guid.NewGuid();
        var add = Attach("D005");
        Assert.True(Validator.Validate(new CorrectAppraisalDocumentsCommand(id, "r", null, add)).IsValid);
        Assert.True(Validator.Validate(new CorrectAppraisalDocumentsCommand(id, "r", Guid.NewGuid(), null)).IsValid);
        Assert.True(Validator.Validate(new CorrectAppraisalDocumentsCommand(id, "r", Guid.NewGuid(), add)).IsValid);
    }

    // ── Dapper needs a DbConnection for its async calls, so NSubstitute's IDbConnection will not do ──

    private sealed class FakeConnection(
        bool typeIsValid, int? maxSortOrder, string? storedFileName, string storedMimeType) : DbConnection
    {
        private readonly Queue<object?> _scalars = new([typeIsValid, maxSortOrder]);

        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new FakeCommand(_scalars, storedFileName, storedMimeType);
    }

    private sealed class FakeCommand(Queue<object?> scalars, string? storedFileName, string storedMimeType) : DbCommand
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
        public override object? ExecuteScalar() => scalars.Dequeue();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => Substitute.For<DbParameter>();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            // Only the stored-upload lookup reads rows.
            var table = new DataTable();
            table.Columns.Add("FileName", typeof(string));
            table.Columns.Add("MimeType", typeof(string));
            table.Columns.Add("FileSizeBytes", typeof(long));
            if (storedFileName is not null) table.Rows.Add(storedFileName, storedMimeType, 1024L);
            return table.CreateDataReader();
        }
    }
}
