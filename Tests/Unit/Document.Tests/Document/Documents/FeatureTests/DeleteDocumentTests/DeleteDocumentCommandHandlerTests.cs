namespace Document.Tests.Document.Documents.FeatureTests.DeleteDocumentTests;

public class DeleteDocumentCommandHandlerTests : DocumentServiceTestBase
{
    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenDeleteSucceeds()
    {
        // Arrange
        var id = 1L;
        var filePath = "fakepath.pdf";
        var document = global::Document.Documents.Models.Document.Create("REQ", id, "PDF", "abc123.pdf", DateTime.Now, "PREFIX", 1, "", filePath);

        _repo.GetDocumentById(id, true, Arg.Any<CancellationToken>())
            .Returns(document);

        _repo.DeleteDocument(id, Arg.Any<CancellationToken>())
            .Returns(true);

        _repo.DeleteDocument(filePath, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new DeleteDocumentHandler(_service);
        var command = new DeleteDocumentCommand(id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        await _repo.Received(1).GetDocumentById(id, true, Arg.Any<CancellationToken>());
        await _repo.Received(1).DeleteDocument(id, Arg.Any<CancellationToken>());
        await _repo.Received(1).DeleteDocument(filePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDeleteDocumentReturnsFalse()
    {
        // Arrange
        var id = 2L;
        var filePath = "fakepath2.pdf";
        var document = global::Document.Documents.Models.Document.Create("REQ", id, "PDF", "abc123.pdf", DateTime.Now, "PREFIX", 1, "", filePath);


        _repo.GetDocumentById(id, true, Arg.Any<CancellationToken>())
            .Returns(document);

        _repo.DeleteDocument(id, Arg.Any<CancellationToken>())
            .Returns(false);

        _repo.DeleteDocument(filePath, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new DeleteDocumentHandler(_service);
        var command = new DeleteDocumentCommand(id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenGetDocumentByIdFails()
    {
        // Arrange
        var id = 3L;

        _repo.GetDocumentById(id, true, Arg.Any<CancellationToken>())
            .Throws(new Exception("DB Error"));

        var handler = new DeleteDocumentHandler(_service);
        var command = new DeleteDocumentCommand(id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}