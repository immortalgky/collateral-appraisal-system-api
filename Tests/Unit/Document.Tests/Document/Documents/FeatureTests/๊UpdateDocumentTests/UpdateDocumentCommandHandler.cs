namespace Document.Tests.Document.Documents.FeatureTests.UpdateDocumentTests;

public class UpdateDocumentHandlerTests : DocumentServiceTestBase
{
    private readonly UpdateDocumentHandler _handler;

    public UpdateDocumentHandlerTests()
    {
        _handler = new UpdateDocumentHandler(_repo);
    }

    [Fact]
    public async Task Handle_ShouldUpdateComment_WhenDocumentExists()
    {
        // Arrange
        var id = 1L;
        var newComment = "Updated comment";
        var command = new UpdateDocumentCommand(id, newComment);

        var document = global::Document.Documents.Models.Document.Create(
            RelateRequest: "REQ001",
            RelateId: id,
            docType: "PDF",
            filename: "contract.pdf",
            uploadTime: DateTime.Now,
            prefix: "PX",
            set: 1,
            comment: "test file",
            filePath: "upload/contract.pdf"
        );

        _repo.GetDocumentById(id, false, Arg.Any<CancellationToken>())
            .Returns(document);

        _repo.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1); // Assume changes saved

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(newComment, document.Comment); // ✅ ตรวจว่า comment ถูกเปลี่ยน
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryMethods_Correctly()
    {
        // Arrange
        var command = new UpdateDocumentCommand(99, "some comment");

        var document = global::Document.Documents.Models.Document.Create(
            RelateRequest: "REQ001",
            RelateId: 99,
            docType: "PDF",
            filename: "contract.pdf",
            uploadTime: DateTime.Now,
            prefix: "PX",
            set: 1,
            comment: "test file",
            filePath: "upload/contract.pdf"
        );

        _repo.GetDocumentById(command.Id, false, Arg.Any<CancellationToken>())
            .Returns(document);

        _repo.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _repo.Received(1)
            .GetDocumentById(command.Id, false, Arg.Any<CancellationToken>());

        await _repo.Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenDocumentNotFound()
    {
        // Arrange
        var command = new UpdateDocumentCommand(404, "not found");

        _repo.GetDocumentById(command.Id, false, Arg.Any<CancellationToken>())
            .Throws(new DocumentNotFoundException(command.Id));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));

        Assert.Equal($"Request ({command.Id}) not found.", ex.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenSaveChangesFails()
    {
        // Arrange
        var command = new UpdateDocumentCommand(1, "fail save");

        var document = global::Document.Documents.Models.Document.Create(
            RelateRequest: "REQ001",
            RelateId: command.Id,
            docType: "PDF",
            filename: "contract.pdf",
            uploadTime: DateTime.Now,
            prefix: "PX",
            set: 1,
            comment: "test file",
            filePath: "upload/contract.pdf"
        );

        _repo.GetDocumentById(command.Id, false, Arg.Any<CancellationToken>())
            .Returns(document);

        _repo.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Throws(new Exception("Save failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            _handler.Handle(command, CancellationToken.None));

        Assert.Equal("Save failed", ex.Message);
    }
}