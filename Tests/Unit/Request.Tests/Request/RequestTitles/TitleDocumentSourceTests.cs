using Auth.Contracts.Users;
using MediatR;
using NSubstitute;
using Request.Application.Services;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;
using Request.Domain.RequestTitles;
using Request.Extensions;
using Request.Infrastructure.Repositories;
using Shared.Time;

namespace Request.Tests.Request.RequestTitles;

/// <summary>Carried files are stamped Source "PREV"; create keeps the payload's Source on title documents and sync keeps it on update.</summary>
public class TitleDocumentSourceTests
{
    private static RequestTitleDocumentDto Doc(
        string? source, Guid? id = null, string? notes = null, Guid? documentId = null) => new()
    {
        Id = id,
        DocumentId = documentId ?? Guid.NewGuid(),
        DocumentType = "D001",
        FileName = "a.pdf",
        Source = source,
        UploadedBy = "u",
        Notes = notes
    };

    private static RequestTitleDto Title(params RequestTitleDocumentDto[] docs) => new()
    {
        CollateralType = "01",
        TitleNumber = "A",
        TitleType = "DEED",
        TitleAddress = new AddressDto(null, null, null, null, null, null, null, null, null),
        DopaAddress = new AddressDto(null, null, null, null, null, null, null, null, null),
        Documents = [.. docs]
    };

    [Fact]
    public async Task Create_keeps_the_payload_Source_on_title_documents()
    {
        var service = new CreateRequestService(
            Substitute.For<IDateTimeProvider>(), Substitute.For<IRequestRepository>(),
            Substitute.For<IRequestTitleRepository>(), Substitute.For<IRequestCommentRepository>(),
            Substitute.For<IRequestUnitOfWork>(), Substitute.For<ISender>(), Substitute.For<IUserLookupService>());

        var (request, titles) = await service.CreateRequestAsync(
            new CreateRequestData(
                "New", "UI", new UserInfoDto("U1", "creator"), "Normal", false,
                null, null, null, [Title(Doc("PREV"), Doc("REQUEST"), Doc("FOLLOWUP"), Doc("junk-longer-than-10"), Doc(null))],
                [new RequestDocumentDto(null, Guid.Empty, Guid.NewGuid(), "D001", "a.pdf", null, 1, null, null, "garbage", false, "u", "U", DateTime.Now)],
                null,
                Requestor: new UserInfoDto("U1", "requestor")),
            TestContext.Current.CancellationToken);

        // Whoever calls (request page or LOS): REQUEST / PREV pass; FOLLOWUP is the server's, so a new row is REQUEST.
        Assert.Equal(["PREV", "REQUEST", "REQUEST", "REQUEST", "REQUEST"], titles[0].Documents.Select(d => d.Source));
        Assert.Equal("REQUEST", request.Documents.Single().Source);
    }

    [Fact]
    public async Task Sync_keeps_the_payload_Source_on_new_rows_and_the_stored_one_on_updates()
    {
        var requestId = Guid.NewGuid();
        var existing = TitleFactory.Create("01", Title(Doc("PREV")).ToRequestTitleData() with { RequestId = requestId });
        existing.AddDocument(Doc("PREV").ToTitleDocumentData());
        var stored = existing.Documents.Single();

        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([existing]);

        var titleDto = Title(
            Doc("REQUEST", stored.Id, notes: "edited", documentId: stored.DocumentId), // same file: payload Source ignored, stored PREV stays
            Doc("PREV"));                               // new row: payload Source is kept
        titleDto = titleDto with { Id = existing.Id };

        await new RequestSyncService(repository, Substitute.For<IDateTimeProvider>()).SyncTitlesAsync(
            requestId, [titleDto], TestContext.Current.CancellationToken);

        var docs = existing.Documents.ToList();
        Assert.Equal("edited", docs.Single(d => d.Id == stored.Id).Notes);
        Assert.Equal("PREV", docs.Single(d => d.Id == stored.Id).Source);
        Assert.Equal(2, docs.Count);
        Assert.All(docs, d => Assert.Equal("PREV", d.Source));
    }

    [Theory]
    [InlineData("REQUEST", "REQUEST")]
    [InlineData(null, "REQUEST")] // payload names none: defaults to REQUEST
    public async Task Replacing_the_file_of_a_carried_title_row_re_stamps_its_Source(string? payloadSource, string expected)
    {
        var (existing, stored, service) = await SeedCarriedTitleDocAsync();

        await service.SyncTitlesAsync(existing.RequestId, [Title(Doc(payloadSource, stored.Id)) with { Id = existing.Id }],
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, existing.Documents.Single().Source);
    }

    [Fact]
    public async Task Same_file_keeps_the_stored_Source_even_when_the_payload_differs()
    {
        var (existing, stored, service) = await SeedCarriedTitleDocAsync();

        await service.SyncTitlesAsync(existing.RequestId,
            [Title(Doc("REQUEST", stored.Id, notes: "edited", documentId: stored.DocumentId)) with { Id = existing.Id }],
            TestContext.Current.CancellationToken);

        Assert.Equal("PREV", existing.Documents.Single().Source);
    }

    [Fact]
    public async Task Replacing_the_file_of_a_carried_request_row_re_stamps_its_Source()
    {
        var request = Domain.Requests.Request.Create(new Domain.Requests.RequestData(
            "01", "UI", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
            DateTime.Now, "Normal", false));
        var carried = request.AddDocument(new Domain.Requests.RequestDocumentData(
            Guid.NewGuid(), "D001", "a.pdf", null, 1, null, null, "PREV", false, "u", "U", DateTime.Now));

        // Ids are assigned by EF on save; give the unsaved row one so the sync can match it.
        var idProperty = typeof(Domain.Requests.RequestDocument).GetProperty("Id")!;
        idProperty.DeclaringType!.GetProperty("Id")!.GetSetMethod(true)!.Invoke(carried, [Guid.NewGuid()]);

        RequestDocumentDto Dto(Guid documentId, string? source) => new(
            carried.Id, request.Id, documentId, "D001", "b.pdf", null, 1, null, null, source, false, "u", "U", DateTime.Now);

        var service = new RequestSyncService(Substitute.For<IRequestTitleRepository>(), Substitute.For<IDateTimeProvider>());

        await service.SyncDocumentsAsync(request, [Dto(carried.DocumentId!.Value, "REQUEST")],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("PREV", request.Documents.Single().Source); // same file: preserved

        await service.SyncDocumentsAsync(request, [Dto(Guid.NewGuid(), "REQUEST")],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("REQUEST", request.Documents.Single().Source); // different file: payload wins

        await service.SyncDocumentsAsync(request, [Dto(Guid.NewGuid(), null)],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("REQUEST", request.Documents.Single().Source); // none named: default
    }

    private static async Task<(RequestTitle Title, TitleDocument Doc, RequestSyncService Service)> SeedCarriedTitleDocAsync()
    {
        var requestId = Guid.NewGuid();
        var existing = TitleFactory.Create("01", Title(Doc("PREV")).ToRequestTitleData() with { RequestId = requestId });
        existing.AddDocument(Doc("PREV").ToTitleDocumentData());
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([existing]);
        await Task.CompletedTask;
        return (existing, existing.Documents.Single(), new RequestSyncService(repository, Substitute.For<IDateTimeProvider>()));
    }

    [Fact]
    public async Task A_FOLLOWUP_row_keeps_its_Source_when_its_file_changes()
    {
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        title.AddDocument(Doc("FOLLOWUP").ToTitleDocumentData());
        var stored = title.Documents.Single();
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);

        // Integration data-fix resubmit: forcedSource REQUEST applies to new rows only.
        await new RequestSyncService(repository, Substitute.For<IDateTimeProvider>()).SyncTitlesAsync(requestId,
            [Title(Doc("REQUEST", stored.Id)) with { Id = title.Id }],
            TestContext.Current.CancellationToken, forcedSource: "REQUEST");

        Assert.Equal("FOLLOWUP", title.Documents.Single().Source);
    }

    [Fact]
    public async Task A_FOLLOWUP_request_row_keeps_its_Source_when_its_file_changes()
    {
        var request = Domain.Requests.Request.Create(new Domain.Requests.RequestData(
            "01", "UI", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
            DateTime.Now, "Normal", false));
        var row = request.AddDocument(new Domain.Requests.RequestDocumentData(
            Guid.NewGuid(), "D001", "a.pdf", null, 1, null, null, "FOLLOWUP", false, "u", "U", DateTime.Now));
        typeof(Domain.Requests.RequestDocument).GetProperty("Id")!.DeclaringType!.GetProperty("Id")!
            .GetSetMethod(true)!.Invoke(row, [Guid.NewGuid()]);

        await new RequestSyncService(Substitute.For<IRequestTitleRepository>(), Substitute.For<IDateTimeProvider>()).SyncDocumentsAsync(
            request,
            [new RequestDocumentDto(row.Id, request.Id, Guid.NewGuid(), "D001", "b.pdf", null, 1, null, null,
                "REQUEST", false, "u", "U", DateTime.Now)],
            TestContext.Current.CancellationToken, forcedSource: "REQUEST");

        Assert.Equal("FOLLOWUP", request.Documents.Single().Source);
    }

    [Fact]
    public async Task An_empty_saved_request_slot_receiving_a_carried_file_takes_the_payload_Source()
    {
        var request = Domain.Requests.Request.Create(new Domain.Requests.RequestData(
            "01", "UI", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
            DateTime.Now, "Normal", false));
        var slot = request.AddDocument(new Domain.Requests.RequestDocumentData(
            null, "D001", null, null, 1, null, null, "REQUEST", true, null, null, null));
        typeof(Domain.Requests.RequestDocument).GetProperty("Id")!.DeclaringType!.GetProperty("Id")!
            .GetSetMethod(true)!.Invoke(slot, [Guid.NewGuid()]);

        await new RequestSyncService(Substitute.For<IRequestTitleRepository>(), Substitute.For<IDateTimeProvider>()).SyncDocumentsAsync(
            request,
            [new RequestDocumentDto(slot.Id, request.Id, Guid.NewGuid(), "D001", "a.pdf", null, 1, null, null,
                "PREV", true, "u", "U", DateTime.Now)],
            TestContext.Current.CancellationToken);

        Assert.Equal("PREV", request.Documents.Single().Source);
    }

    [Fact]
    public async Task An_empty_saved_title_slot_receiving_a_carried_file_takes_the_payload_Source()
    {
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        title.AddDocument(new TitleDocumentData { DocumentType = "D001", Source = "REQUEST" });
        var slot = title.Documents.Single();
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);

        await new RequestSyncService(repository, Substitute.For<IDateTimeProvider>()).SyncTitlesAsync(requestId,
            [Title(Doc("PREV", slot.Id)) with { Id = title.Id }], TestContext.Current.CancellationToken);

        Assert.Equal("PREV", title.Documents.Single().Source);
    }

    [Fact]
    public async Task Sync_whitelists_a_payload_Source_on_new_and_swapped_rows_and_never_creates_a_FOLLOWUP_row()
    {
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        title.AddDocument(Doc("PREV").ToTitleDocumentData());
        var carried = title.Documents.Single();
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);

        await new RequestSyncService(repository, Substitute.For<IDateTimeProvider>()).SyncTitlesAsync(requestId,
            [Title(Doc("junk", carried.Id), Doc("junk"), Doc("FOLLOWUP")) with { Id = title.Id }],
            TestContext.Current.CancellationToken);

        var sources = title.Documents.Select(d => d.Source).ToList();
        Assert.Equal(["REQUEST", "REQUEST", "REQUEST"], sources); // swapped PREV row, new junk, new FOLLOWUP claim
    }

    [Fact]
    public async Task A_title_document_without_an_upload_time_gets_the_creation_time_never_the_default_date()
    {
        var created = new DateTime(2026, 10, 7, 9, 30, 0);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(created);
        var service = new CreateRequestService(
            clock, Substitute.For<IRequestRepository>(),
            Substitute.For<IRequestTitleRepository>(), Substitute.For<IRequestCommentRepository>(),
            Substitute.For<IRequestUnitOfWork>(), Substitute.For<ISender>(), Substitute.For<IUserLookupService>());
        var stamped = new DateTime(2025, 1, 2, 3, 4, 5);

        var (_, titles) = await service.CreateRequestAsync(
            new CreateRequestData(
                "New", "UI", new UserInfoDto("U1", "creator"), "Normal", false,
                null, null, null,
                [Title(Doc("PREV") with { UploadedAt = stamped }, Doc("REQUEST"), new RequestTitleDocumentDto { DocumentType = "D005" })],
                null, null, Requestor: new UserInfoDto("U1", "requestor")),
            TestContext.Current.CancellationToken);

        // A file with no upload time gets the creation time; an empty placeholder keeps the default (the column is NOT NULL).
        Assert.Equal([stamped, created, default(DateTime)], titles[0].Documents.Select(d => d.UploadedAt));
    }

    [Fact]
    public async Task A_client_cannot_turn_a_stored_row_into_FOLLOWUP_by_swapping_its_file()
    {
        var (existing, stored, service) = await SeedCarriedTitleDocAsync(); // stored PREV

        await service.SyncTitlesAsync(existing.RequestId, [Title(Doc("FOLLOWUP", stored.Id)) with { Id = existing.Id }],
            TestContext.Current.CancellationToken);

        Assert.Equal("REQUEST", existing.Documents.Single().Source);
    }

    [Fact]
    public async Task Recreating_a_title_keeps_the_label_of_a_stored_FOLLOWUP_file_the_client_echoes_but_no_other()
    {
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        var followUpFile = Guid.NewGuid();
        title.AddDocument(Doc("FOLLOWUP", documentId: followUpFile).ToTitleDocumentData());
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);

        // Collateral type changes 01 -> 02: the title and its rows are re-created from the payload.
        var changed = Title(Doc("FOLLOWUP", documentId: followUpFile), Doc("FOLLOWUP")) with
        {
            Id = title.Id,
            CollateralType = "02"
        };
        var result = await new RequestSyncService(repository, Substitute.For<IDateTimeProvider>()).SyncTitlesAsync(
            requestId, [changed], TestContext.Current.CancellationToken);

        Assert.Equal(["FOLLOWUP", "REQUEST"], result.Single().Documents.Select(d => d.Source));
    }

    [Fact]
    public async Task Sync_gives_a_file_without_an_upload_time_the_current_time_on_both_levels_and_leaves_placeholders_alone()
    {
        var now = new DateTime(2026, 10, 9, 8, 7, 6);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(now);

        // Title level: update path (stored row gets a file with no time) and create path (new rows).
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        title.AddDocument(new TitleDocumentData { DocumentType = "D001", Source = "REQUEST" });
        var slot = title.Documents.Single();
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);
        var service = new RequestSyncService(repository, clock);

        await service.SyncTitlesAsync(requestId,
            [Title(Doc("REQUEST", slot.Id), Doc("REQUEST"), new RequestTitleDocumentDto { DocumentType = "D005" }) with { Id = title.Id }],
            TestContext.Current.CancellationToken);

        Assert.Equal([now, now, default(DateTime)], title.Documents.Select(d => d.UploadedAt));

        // Request level: a file with the default date gets the time, a stamped one keeps its own, a placeholder stays null.
        var request = Domain.Requests.Request.Create(new Domain.Requests.RequestData(
            "01", "UI", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
            DateTime.Now, "Normal", false));
        var stamped = new DateTime(2025, 1, 2, 3, 4, 5);
        RequestDocumentDto Dto(Guid? documentId, DateTime? at) =>
            new(null, request.Id, documentId, "D001", "a.pdf", null, 1, null, null, "REQUEST", false, "u", "U", at);

        await service.SyncDocumentsAsync(request,
            [Dto(Guid.NewGuid(), default(DateTime)), Dto(Guid.NewGuid(), stamped), Dto(null, null)],
            TestContext.Current.CancellationToken);

        Assert.Equal([now, stamped, null], request.Documents.Select(d => d.UploadedAt));
    }

    [Fact]
    public async Task A_legacy_cased_FollowUp_row_keeps_its_label_when_its_file_changes()
    {
        var request = Domain.Requests.Request.Create(new Domain.Requests.RequestData(
            "01", "UI", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
            DateTime.Now, "Normal", false));
        var row = request.AddDocument(new Domain.Requests.RequestDocumentData(
            Guid.NewGuid(), "D001", "a.pdf", null, 1, null, null, "FollowUp", false, "u", "U", DateTime.Now));
        typeof(Domain.Requests.RequestDocument).GetProperty("Id")!.DeclaringType!.GetProperty("Id")!
            .GetSetMethod(true)!.Invoke(row, [Guid.NewGuid()]);

        await new RequestSyncService(Substitute.For<IRequestTitleRepository>(), Substitute.For<IDateTimeProvider>())
            .SyncDocumentsAsync(request,
                [new RequestDocumentDto(row.Id, request.Id, Guid.NewGuid(), "D001", "b.pdf", null, 1, null, null,
                    "REQUEST", false, "u", "U", DateTime.Now)],
                TestContext.Current.CancellationToken);

        Assert.Equal("FollowUp", request.Documents.Single().Source); // preserved as stored, not relabelled REQUEST
    }

    [Fact]
    public async Task Recreating_a_title_keeps_the_label_of_a_legacy_cased_FollowUp_file_the_client_echoes()
    {
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        var followUpFile = Guid.NewGuid();
        title.AddDocument(Doc("FollowUp", documentId: followUpFile).ToTitleDocumentData());
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);

        var changed = Title(Doc("FollowUp", documentId: followUpFile)) with { Id = title.Id, CollateralType = "02" };
        var result = await new RequestSyncService(repository, Substitute.For<IDateTimeProvider>())
            .SyncTitlesAsync(requestId, [changed], TestContext.Current.CancellationToken);

        Assert.Equal("FOLLOWUP", result.Single().Documents.Single().Source);
    }

    [Fact]
    public async Task Followup_recreation_of_a_title_relabels_only_the_genuinely_new_files()
    {
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        var untouched = Guid.NewGuid();
        var carried = Guid.NewGuid();
        title.AddDocument(Doc("REQUEST", documentId: untouched).ToTitleDocumentData());
        title.AddDocument(Doc("PREV", documentId: carried).ToTitleDocumentData());
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);

        // Collateral type changes in a follow-up resubmit: the title is re-created from the payload.
        var changed = Title(
            Doc("REQUEST", documentId: untouched), Doc("PREV", documentId: carried),
            Doc("REQUEST"), // a genuinely new file
            new RequestTitleDocumentDto { DocumentType = "D005" }) with { Id = title.Id, CollateralType = "02" };
        var result = await new RequestSyncService(repository, Substitute.For<IDateTimeProvider>())
            .SyncTitlesAsync(requestId, [changed], TestContext.Current.CancellationToken, forcedSource: "FOLLOWUP");

        // untouched and carried files keep their labels; the new file answers the follow-up; the empty slot does not.
        Assert.Equal(["REQUEST", "PREV", "FOLLOWUP", "REQUEST"], result.Single().Documents.Select(d => d.Source));
    }

    [Fact]
    public async Task Followup_mode_does_not_stamp_FOLLOWUP_on_rows_that_end_up_without_a_file()
    {
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        title.AddDocument(Doc("REQUEST").ToTitleDocumentData());
        var emptied = title.Documents.Single();
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);

        // The stored row's file is removed (DocumentId -> null) and an empty slot is added.
        var emptiedDto = Doc("REQUEST", emptied.Id) with { DocumentId = null, FileName = null };
        await new RequestSyncService(repository, Substitute.For<IDateTimeProvider>()).SyncTitlesAsync(requestId,
            [Title(emptiedDto, new RequestTitleDocumentDto { DocumentType = "D005" }) with { Id = title.Id }],
            TestContext.Current.CancellationToken, forcedSource: "FOLLOWUP");

        Assert.All(title.Documents, d => Assert.Equal("REQUEST", d.Source));

        // Request level: a new file takes the forced label, a new empty slot does not.
        var request = Domain.Requests.Request.Create(new Domain.Requests.RequestData(
            "01", "UI", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
            DateTime.Now, "Normal", false));
        RequestDocumentDto Dto(Guid? documentId) =>
            new(null, request.Id, documentId, "D001", null, null, 1, null, null, "REQUEST", false, "u", "U", DateTime.Now);
        await new RequestSyncService(Substitute.For<IRequestTitleRepository>(), Substitute.For<IDateTimeProvider>())
            .SyncDocumentsAsync(request, [Dto(Guid.NewGuid()), Dto(null)],
                TestContext.Current.CancellationToken, forcedSource: "FOLLOWUP");

        Assert.Equal(["FOLLOWUP", "REQUEST"], request.Documents.Select(d => d.Source));
    }

    [Fact]
    public async Task A_notes_only_edit_keeps_the_stored_upload_time_when_the_payload_omits_it()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(new DateTime(2026, 10, 9, 8, 7, 6));
        var stamped = new DateTime(2025, 1, 2, 3, 4, 5);
        var requestId = Guid.NewGuid();
        var title = TitleFactory.Create("01", Title().ToRequestTitleData() with { RequestId = requestId });
        var file = Guid.NewGuid();
        title.AddDocument(Doc("REQUEST", documentId: file).ToTitleDocumentData() with { UploadedAt = stamped });
        var stored = title.Documents.Single();
        var repository = Substitute.For<IRequestTitleRepository>();
        repository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns([title]);

        // Same DocumentId, new notes, UploadedAt left out (default).
        await new RequestSyncService(repository, clock).SyncTitlesAsync(requestId,
            [Title(Doc("REQUEST", stored.Id, notes: "edited", documentId: file)) with { Id = title.Id }],
            TestContext.Current.CancellationToken);

        Assert.Equal("edited", title.Documents.Single().Notes);
        Assert.Equal(stamped, title.Documents.Single().UploadedAt);
    }
}
