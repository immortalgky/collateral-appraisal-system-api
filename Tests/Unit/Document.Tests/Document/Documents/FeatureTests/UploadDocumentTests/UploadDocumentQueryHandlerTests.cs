using Document.Contracts.Documents.Dtos;
using Document.Documents.Features.UploadDocument;
using NSubstitute.ExceptionExtensions;

namespace Document.Tests.Document.Documents.FeatureTests.UploadDocumentTests;

public class UploadDocumentQueryHandlerTests : DocumentServiceTestBase
{
    private readonly IDocumentService _documentService = Substitute.For<IDocumentService>();

    [Fact]
    public async Task Should_Handle_Success()
    {
        var documentService = Substitute.For<IDocumentService>();

        var result = new UploadDocumentResult(new List<UploadResultDto>
        {
            new(true, "Uploaded")
        });

        var file = CreateMockFile("test.pdf", new byte[1024]);

        documentService
            .UploadAsync(Arg.Any<IReadOnlyList<IFormFile>>(), "REQ", 123, Arg.Any<CancellationToken>())
            .Returns(result);

        var handler = new UploadDocumentCommandHandler(documentService);
        var command = new UploadDocumentCommand([file], "REQ", 123);

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.Single(response.Result);
        Assert.True(response.Result[0].IsSuccess);
    }

    [Fact]
    public async Task Should_Handle_EmptyFileList()
    {
        var documentService = Substitute.For<IDocumentService>();

        var result = new UploadDocumentResult(new List<UploadResultDto>());

        documentService
            .UploadAsync(Arg.Any<IReadOnlyList<IFormFile>>(), "REQ", 999, Arg.Any<CancellationToken>())
            .Returns(result);

        var handler = new UploadDocumentCommandHandler(documentService);
        var command = new UploadDocumentCommand([], "REQ", 999);

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.Empty(response.Result);
    }

    // [Fact]
    // public async Task Should_Throw_When_Cancelled()
    // {
    //     var documentService = Substitute.For<IDocumentService>();

    //     var file = CreateMockFile("cancel.pdf", new byte[1024]);
    //     var cts = new CancellationTokenSource();
    //     cts.Cancel();

    //     var handler = new UploadDocumentCommandHandler(documentService);
    //     var command = new UploadDocumentCommand([file], "REQ", 777);

    //     await Assert.ThrowsAsync<TaskCanceledException>(() =>
    //         handler.Handle(command, cts.Token));
    // }

    [Fact]
    public async Task Should_Throw_When_ServiceFails()
    {
        var documentService = Substitute.For<IDocumentService>();

        var file = CreateMockFile("error.pdf", new byte[1024]);

        documentService
            .UploadAsync(Arg.Any<IReadOnlyList<IFormFile>>(), "REQ", 888, Arg.Any<CancellationToken>())
            .Throws(new Exception("Service error"));

        var handler = new UploadDocumentCommandHandler(documentService);
        var command = new UploadDocumentCommand([file], "REQ", 888);

        await Assert.ThrowsAsync<Exception>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}