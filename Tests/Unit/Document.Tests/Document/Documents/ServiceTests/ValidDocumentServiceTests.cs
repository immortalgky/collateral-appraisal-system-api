namespace Document.Tests.Document.Documents.ServiceTests;

public class ValidDocumentServiceTests : DocumentServiceTestBase
{
    [Fact]
    public async Task Valid_UploadpdfFileUnder5Mb()
    {
        var file = CreateMockFile("file1.pdf", GenerateBytes(1 * 1024 * 1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 100, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }

    [Fact]
    public async Task Valid_UploadPDFFileUnder5Mb()
    {
        var file = CreateMockFile("file1.PDF", GenerateBytes(1 * 1024 * 1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 101, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }


    [Fact]
    public async Task Valid_Upload5FilesUnder5Mb()
    {
        var files = Enumerable.Range(1, 5)
            .Select(i => CreateMockFile($"file_{i}.pdf", GenerateBytes(1 * 1024 * 1024)))
            .ToList();

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(files, "Request", 102, CancellationToken.None);

        Assert.Equal(5, result.Result.Count);
        Assert.All(result.Result, r => Assert.True(r.IsSuccess));
    }

    [Fact]
    public async Task Valid_UploadFileExactly5Mb()
    {
        var file = CreateMockFile("file1.pdf", GenerateBytes(5 * 1024 * 1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 103, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }

    [Fact]
    public async Task Valid_UploadFileSpecialCharactersUnder5Mb()
    {
        var file = CreateMockFile("!@#$%^&().pdf", GenerateBytes(1 * 1024 * 1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 104, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }

    [Fact]
    public async Task Valid_UploadFileThaiCharactersUnder5Mb()
    {
        var file = CreateMockFile("เอกสาร.pdf", GenerateBytes(1 * 1024 * 1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 105, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }

    [Fact]
    public async Task Valid_UploadFileJapaneseCharactersUnder5Mb()
    {
        var file = CreateMockFile("ドキュメント.pdf", GenerateBytes(1 * 1024 * 1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 106, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }

    [Fact]
    public async Task Valid_UploadFileWithSpaces()
    {
        var file = CreateMockFile("my file.pdf", GenerateBytes(1 * 1024 * 1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 107, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }

    [Fact]
    public async Task Valid_UploadFilesWithSameName()
    {
        var file1 = CreateMockFile("a.pdf", GenerateBytes(1 * 1024 * 1024 + 2));
        var file2 = CreateMockFile("a.pdf", GenerateBytes(1 * 1024 * 1024 + 1));

        var files = new List<IFormFile> { file1, file2 };

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromResult(false),
                    Task.FromResult(true));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(files, "request", 108, CancellationToken.None);

        Assert.Equal(2, result.Result.Count);
        Assert.True(result.Result[0].IsSuccess);
        Assert.False(result.Result[1].IsSuccess);
        Assert.Contains("Duplicate", result.Result[1].Comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Valid_UploadFilesWithSameContent()
    {
        var file1 = CreateMockFile("a.pdf", GenerateBytes(1 * 1024 * 1024));
        var file2 = CreateMockFile("b.pdf", GenerateBytes(1 * 1024 * 1024));

        var files = new List<IFormFile> { file1, file2 };

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromResult(false),
                    Task.FromResult(true));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(files, "request", 108, CancellationToken.None);

        Assert.Equal(2, result.Result.Count);
        Assert.True(result.Result[0].IsSuccess);
        Assert.False(result.Result[1].IsSuccess);
        Assert.Contains("Duplicate", result.Result[1].Comment, StringComparison.OrdinalIgnoreCase);
    }
}