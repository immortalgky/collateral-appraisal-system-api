namespace Document.Tests.Document.Documents.FeatureTests.GetDocumentByIdTests;

public class GetDocumentByIdHandlerTests : DocumentServiceTestBase
{
    private readonly GetDocumentByIdHandler _handler;

    public GetDocumentByIdHandlerTests()
    {
        _handler = new GetDocumentByIdHandler(_repo);
    }
    [Fact]
    public async Task Handle_ShouldReturnDocumentDto_WhenDocumentExists()
    {
        // Arrange
        var id = 1L;
        var query = new GetDocumentByIdQuery(id);


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

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Document);
        Assert.Equal(id, result.Document.RelateId);
        Assert.Equal("contract.pdf", result.Document.Filename);
        Assert.Equal("REQ001", result.Document.RelateRequest);
    }

    [Fact]
    public async Task Handle_ShouldCallRepository_WithCorrectParameters()
    {
        // Arrange
        var id = 5L;
        var query = new GetDocumentByIdQuery(id);
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

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        await _repo.Received(1).GetDocumentById(id, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenDocumentNotFound()
    {
        // Arrange
        var id = 999L;
        var query = new GetDocumentByIdQuery(id);

        _repo.GetDocumentById(id, false, Arg.Any<CancellationToken>())
            .Throws(new DocumentNotFoundException(id));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None));

        Assert.Equal($"Request ({id}) not found.", ex.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrowGeneralException_WhenRepositoryFails()
    {
        // Arrange
        var id = 77L;
        var query = new GetDocumentByIdQuery(id);

        _repo.GetDocumentById(id, false, Arg.Any<CancellationToken>())
            .Throws(new Exception("Unexpected DB error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            _handler.Handle(query, CancellationToken.None));

        Assert.Equal("Unexpected DB error", ex.Message);
    }
}