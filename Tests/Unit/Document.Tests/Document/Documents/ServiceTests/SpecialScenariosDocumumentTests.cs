namespace Document.Tests.Document.Documents.ServiceTests;

public class SpecialUploadScenariosTests : DocumentServiceTestBase
{
    [Fact]
    public async Task ShouldFail_UploadRepositoryTimeout()
    {
        var file = CreateMockFile("timeout.pdf", GenerateBytes(1024));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(6000, ci.Arg<CancellationToken>());
                return true;
            });

        using var cts = new CancellationTokenSource(10);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _service.UploadAsync([file], "Request", 302, cts.Token));
    }

    [Fact]
    public async Task ShouldFail_SHA256Throws()
    {
        var stream = new ThrowingStream();
        var file = new FormFile(stream, 0, 1024, "file", "shaerror.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var ex = await Assert.ThrowsAsync<IOException>(() =>
            _service.UploadAsync([file], "Request", 304, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ShouldFail_RepositoryAlreadyHasDocument()
    {
        var file = CreateMockFile("duplicate.pdf", GenerateBytes(1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.UploadAsync([file], "Request", 306, TestContext.Current.CancellationToken);

        Assert.False(result.Result[0].IsSuccess);
        Assert.Contains("duplicate", result.Result[0].Comment, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ShouldFail_CopyToThrowsIOException()
    {
        var stream = new ThrowingStream();
        var file = new FormFile(stream, 0, 1024, "file", "throwcopy.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var ex = await Assert.ThrowsAsync<IOException>(() =>
            _service.UploadAsync([file], "Request", 308, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ShouldHandle_SameFileNameDifferentContent()
    {
        var file1 = CreateMockFile("same.pdf", GenerateBytes(1024));
        var file2 = CreateMockFile("same.pdf", GenerateBytes(2048));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.UploadAsync([file1, file2], "Request", 309, TestContext.Current.CancellationToken);

        Assert.All(result.Result, r => Assert.True(r.IsSuccess));
    }


    [Fact]
    public async Task ShouldAllow_EmptyComment()
    {
        var file = CreateMockFile("comment.pdf", GenerateBytes(1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _repo.UploadDocument(Arg.Do<global::Document.Documents.Models.Document>(doc => Assert.Equal("", doc.Comment)), Arg.Any<CancellationToken>())
            .Returns(true);

        await _service.UploadAsync([file], "Request", 311, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ShouldHandle_MultipleRetrySameFile()
    {
        var file = CreateMockFile("retry.pdf", GenerateBytes(1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(true);

        for (int i = 0; i < 4; i++)
        {
            var result = await _service.UploadAsync([file], "Request", 312, TestContext.Current.CancellationToken);
            Assert.True(result.Result[0].IsSuccess);
        }
    }

    private class ThrowingStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count)
            => throw new IOException("Simulated read failure");
    }
}
