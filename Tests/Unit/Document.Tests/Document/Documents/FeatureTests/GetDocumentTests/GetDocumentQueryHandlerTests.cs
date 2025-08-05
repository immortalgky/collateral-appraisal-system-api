using System.Reflection.Metadata;
using Document.Documents.Features.GetDocuments;

namespace Document.Tests.Document.Documents.FeatureTests.GetDocumentTests;

public class GetDocumentQueryHandlerTests : DocumentServiceTestBase
{
    [Fact]
    public async Task Handle_ShouldReturnMappedDocuments()
    {
        // Arrange
        var documents = new List<global::Document.Documents.Models.Document>
        {
            global::Document.Documents.Models.Document.Create("REQ1", 1, "TYPE1", "file1.pdf", DateTime.UtcNow, "PX1", 1, "Note1", "/path/file1.pdf"),
            global::Document.Documents.Models.Document.Create("REQ2", 2, "TYPE2", "file2.pdf", DateTime.UtcNow, "PX2", 2, "Note2", "/path/file2.pdf")
        };

        _repo.GetDocuments(Arg.Any<CancellationToken>())
            .Returns(documents);

        var handler = new GetDocumentHandler(_repo);
        var query = new GetDocumentQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Documents.Count);
        Assert.Equal("file1.pdf", result.Documents[0].Filename);
        Assert.Equal("file2.pdf", result.Documents[1].Filename);
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryOnce()
    {
        // Arrange
        _repo.GetDocuments(Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = new GetDocumentHandler(_repo);
        var query = new GetDocumentQuery();

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert
        await _repo.Received(1).GetDocuments(Arg.Any<CancellationToken>());
    }
}