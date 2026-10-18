using Document;
using Document.Domain.Documents;
using Document.Services;
using FluentAssertions;
using Integration.Application.Features.Documents.DeleteDocument;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Configurations;
using Shared.Data;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Time;
using DocumentEntity = Document.Domain.Documents.Models.Document;

namespace Integration.Tests;

/// <summary>
/// Both delete endpoints share one path (DocumentService.DeleteFileAsync) and one rule on the Document
/// aggregate: a live link anywhere (from document.vw_DocumentLinks, not the drifting ReferenceCount) -> 409,
/// otherwise a soft delete — the file stays on the share, the row is not removed, ReferenceCount is untouched.
/// A repeated delete is a no-op; an unknown id is 404.
/// </summary>
public class DocumentDeleteRuleTests : IDisposable
{
    private readonly string _file = Path.GetTempFileName();

    public void Dispose() => File.Delete(_file);

    private DocumentEntity NewDocument() => DocumentEntity.Create(
        Guid.NewGuid(), Guid.NewGuid(), "D001", "Request", "a.pdf", ".pdf", 10, "application/pdf",
        _file, "/upload/a.pdf", "u", "U", DateTime.Now, null, null, null, "chk", "SHA256");

    private static IDocumentLinkChecker Links(bool linked)
    {
        var checker = Substitute.For<IDocumentLinkChecker>();
        checker.HasLiveLinksAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(linked);
        return checker;
    }

    private static IRepository<DocumentEntity, Guid> RepositoryWith(DocumentEntity? document)
    {
        var repository = Substitute.For<IRepository<DocumentEntity, Guid>>();
        if (document is not null)
            repository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        return repository;
    }

    private static readonly DateTime Clock = new(2026, 10, 8, 14, 0, 0);

    private static DocumentService NewService(IRepository<DocumentEntity, Guid> repository, bool linked)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(Clock);
        var user = Substitute.For<ICurrentUserService>();
        user.Username.Returns("tester");
        var uow = Substitute.For<IDocumentUnitOfWork>();
        uow.Repository<DocumentEntity, Guid>().Returns(repository);
        uow.Repository<Document.Domain.UploadSessions.Model.UploadSession, Guid>()
            .Returns(Substitute.For<IRepository<Document.Domain.UploadSessions.Model.UploadSession, Guid>>());
        return new DocumentService(
            user, uow, Substitute.For<IWebHostEnvironment>(),
            Options.Create(new FileStorageConfiguration()), Substitute.For<IImageResizeService>(),
            Substitute.For<IChunkedUploadStore>(), NullLogger<DocumentService>.Instance,
            clock, Links(linked));
    }

    // The Integration endpoint is the same service behind a handler.
    private static Task IntegrationDelete(DocumentService service, Guid id) =>
        new DeleteDocumentCommandHandler(service)
            .Handle(new DeleteDocumentCommand(id), TestContext.Current.CancellationToken);

    private static Task ServiceDelete(DocumentService service, Guid id) =>
        service.DeleteFileAsync(id, TestContext.Current.CancellationToken);

    public static TheoryData<bool> Callers => [true, false];

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task A_linked_document_is_a_conflict_and_changes_nothing(bool viaIntegration)
    {
        var document = NewDocument();
        var repository = RepositoryWith(document);
        var service = NewService(repository, linked: true);

        var act = () => viaIntegration ? IntegrationDelete(service, document.Id) : ServiceDelete(service, document.Id);

        await act.Should().ThrowAsync<ConflictException>();
        document.IsDeleted.Should().BeFalse();
        File.Exists(_file).Should().BeTrue();
        await repository.DidNotReceive().UpdateAsync(Arg.Any<DocumentEntity>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task An_unlinked_document_is_soft_deleted_and_the_file_and_row_stay(bool viaIntegration)
    {
        var document = NewDocument();
        var repository = RepositoryWith(document);
        var service = NewService(repository, linked: false);

        await (viaIntegration ? IntegrationDelete(service, document.Id) : ServiceDelete(service, document.Id));

        document.IsDeleted.Should().BeTrue();
        document.DeletedBy.Should().Be("tester");
        document.DeletedAt.Should().Be(Clock); // the injected clock, not DateTime.Now
        document.ReferenceCount.Should().Be(0);
        File.Exists(_file).Should().BeTrue();
        await repository.Received(1).UpdateAsync(document, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().DeleteAsync(Arg.Any<DocumentEntity>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task An_unknown_id_is_not_found(bool viaIntegration)
    {
        var service = NewService(RepositoryWith(null), linked: false);
        var id = Guid.NewGuid();

        var act = () => viaIntegration ? IntegrationDelete(service, id) : ServiceDelete(service, id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task Deleting_an_already_deleted_document_succeeds_and_keeps_who_deleted_it(bool viaIntegration)
    {
        var document = NewDocument();
        document.Delete("first", isLinked: false, deletedAt: Clock.AddDays(-1));
        var repository = RepositoryWith(document);
        var service = NewService(repository, linked: true); // not even asked

        await (viaIntegration ? IntegrationDelete(service, document.Id) : ServiceDelete(service, document.Id));

        document.DeletedBy.Should().Be("first");
        document.DeletedAt.Should().Be(Clock.AddDays(-1));
        await repository.DidNotReceive().UpdateAsync(Arg.Any<DocumentEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_stale_ReferenceCount_does_not_decide_the_outcome()
    {
        var document = NewDocument();
        document.Link(DateTime.Now); // counter says linked, but nothing live links it
        var service = NewService(RepositoryWith(document), linked: false);

        await ServiceDelete(service, document.Id);

        document.IsDeleted.Should().BeTrue();
        document.ReferenceCount.Should().Be(1);
    }
}
