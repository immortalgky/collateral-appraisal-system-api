using Document.Contracts.Documents.Dtos;
using Document.Domain.Documents;
using Document.Domain.Documents.Features.GetDocumentById;
using Document.Domain.Documents.Features.GetDocuments;
using FluentAssertions;
using Mapster;
using NSubstitute;
using Shared.Exceptions;
using DocumentEntity = Document.Domain.Documents.Models.Document;

namespace Document.Tests;

public class GetDocumentDtoMappingTests
{
    private static DocumentEntity NewDocument(Guid? id = null) =>
        DocumentEntity.Create(
            id ?? Guid.NewGuid(), Guid.NewGuid(), "ValuationReport", "Appraisal",
            "report.pdf", ".pdf", 1234, "application/pdf",
            "/var/data/secret/report.pdf", "/documents/report.pdf",
            "u001", "Somchai", new DateTime(2026, 10, 1, 9, 30, 0),
            "note", null, null, null, null);

    [Fact]
    public void Adapt_maps_entity_to_dto_without_exposing_storage_path()
    {
        var document = NewDocument();

        var dto = document.Adapt<DocumentDto>();

        dto.Id.Should().Be(document.Id);
        dto.DocumentType.Should().Be("ValuationReport");
        dto.DocumentCategory.Should().Be("Appraisal");
        dto.FileName.Should().Be("report.pdf");
        dto.FileExtension.Should().Be(".pdf");
        dto.FileSizeBytes.Should().Be(1234);
        dto.MimeType.Should().Be("application/pdf");
        dto.StorageUrl.Should().Be("/documents/report.pdf");
        dto.UploadedBy.Should().Be("u001");
        dto.UploadedByName.Should().Be("Somchai");
        dto.UploadedAt.Should().Be(new DateTime(2026, 10, 1, 9, 30, 0));
        dto.Description.Should().Be("note");
        dto.IsActive.Should().BeTrue();
        typeof(DocumentDto).GetProperty("StoragePath").Should().BeNull();
    }

    [Fact]
    public async Task GetDocumentById_returns_mapped_dto()
    {
        var document = NewDocument();
        var repository = Substitute.For<IDocumentRepository>();
        repository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);

        var result = await new GetDocumentByIdHandler(repository)
            .Handle(new GetDocumentByIdQuery(document.Id), CancellationToken.None);

        result.Document.Id.Should().Be(document.Id);
        result.Document.FileName.Should().Be("report.pdf");
    }

    [Fact]
    public async Task GetDocumentById_throws_NotFound_when_missing_or_deleted()
    {
        var deleted = NewDocument();
        deleted.Delete("u001");
        var repository = Substitute.For<IDocumentRepository>();
        repository.GetByIdAsync(deleted.Id, Arg.Any<CancellationToken>()).Returns(deleted);
        var handler = new GetDocumentByIdHandler(repository);

        await handler.Invoking(h => h.Handle(new GetDocumentByIdQuery(Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
        await handler.Invoking(h => h.Handle(new GetDocumentByIdQuery(deleted.Id), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetDocuments_returns_mapped_list()
    {
        var documents = new[] { NewDocument(), NewDocument() };
        var repository = Substitute.For<IDocumentRepository>();
        repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(documents);

        var result = await new GetDocumentHandler(repository)
            .Handle(new GetDocumentQuery(), CancellationToken.None);

        result.Documents.Select(d => d.Id).Should().BeEquivalentTo(documents.Select(d => d.Id));
    }
}
