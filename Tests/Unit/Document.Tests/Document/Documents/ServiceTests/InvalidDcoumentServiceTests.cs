using System.Security.Cryptography;
using Document.Documents.Exceptions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Document.Tests.Document.Documents.ServiceTests;

public class InvalidDocumentServiceTests : DocumentServiceTestBase
{
    [Fact]
    public async Task ShouldFail_UploadFileWithoutExtension()
    {
        var file = CreateMockFile("noextension", GenerateBytes(1024));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 201, CancellationToken.None);

        Assert.False(result.Result[0].IsSuccess);

        Assert.Contains("extension", result.Result[0].Comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldFail_UploadTxtFile()
    {
        var file = CreateMockFile("note.txt", GenerateBytes(1024));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 201, CancellationToken.None);

        Assert.False(result.Result[0].IsSuccess);

        Assert.Contains("extension", result.Result[0].Comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldFail_UploadFileOver5Mb()
    {
        var file = CreateMockFile("too_big.pdf", GenerateBytes(5 * 1024 * 1024 + 1));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 201, CancellationToken.None);

        Assert.False(result.Result[0].IsSuccess);

        Assert.Contains("exceeded", result.Result[0].Comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldFail_UploadZeroByteFile()
    {
        var file = CreateMockFile("empty.pdf", []);

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 201, CancellationToken.None);

        Assert.False(result.Result[0].IsSuccess);

        Assert.Contains("empty", result.Result[0].Comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldFail_UploadMoreThan5Files()
    {
        var files = Enumerable.Range(0, 6)
            .Select(i => CreateMockFile($"file{i}.pdf", GenerateBytes(1024)))
            .ToList();

        var ex = await Assert.ThrowsAsync<UploadDocumentException>(() =>
            _service.UploadAsync(files, "Request", 205, CancellationToken.None));

        Assert.Contains("limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldFail_UploadDuplicateFileContent()
    {
        var file1 = CreateMockFile("a.pdf", GenerateBytes(1024));
        var file2 = CreateMockFile("b.pdf", GenerateBytes(1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false), Task.FromResult(true));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file1, file2 }, "Request", 206, CancellationToken.None);

        Assert.True(result.Result[0].IsSuccess);
        Assert.False(result.Result[1].IsSuccess);
        Assert.Contains("duplicate", result.Result[1].Comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldFail_UploadDuplicateFileNameAndContent()
    {
        var content = GenerateBytes(1024);
        var file1 = CreateMockFile("same.pdf", content);
        var file2 = CreateMockFile("same.pdf", content);

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false), Task.FromResult(true));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file1, file2 }, "Request", 207, CancellationToken.None);

        Assert.True(result.Result[0].IsSuccess);
        Assert.False(result.Result[1].IsSuccess);
    }

    [Fact]
    public async Task ShouldFail_DiskFullException()
    {
        var file = CreateMockFile("diskfull.pdf", GenerateBytes(1024));

        var service = new FaultyFileWriteService(_repo);

        var result = await service.UploadAsync([file], "Request", 208, CancellationToken.None);

        Assert.False(result.Result[0].IsSuccess);

        Assert.Contains("Storage full", result.Result[0].Comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldFail_UploadCancelledToken()
    {
        var file = CreateMockFile("cancel.pdf", GenerateBytes(1024));
        var token = new CancellationTokenSource();
        token.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _service.UploadAsync([file], "Request", 210, token.Token));
    }

    [Fact]
    public async Task ShouldFail_FilePathAlreadyExists()
    {
        var file = CreateMockFile("exists.pdf", GenerateBytes(1024));

        var path = Path.Combine(Directory.GetCurrentDirectory(), "Upload", "exists.pdf");

        File.WriteAllBytes(path, GenerateBytes(1024));

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.UploadAsync([file], "Request", 211, CancellationToken.None);

        Assert.False(result.Result[0].IsSuccess);

        Assert.Contains("Duplicate", result.Result[0].Comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldFail_WithoutFileStream()
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns("file.pdf");
        file.Length.Returns(1024);
        file.OpenReadStream().Returns(x => throw new IOException("No stream"));

        var ex = await Assert.ThrowsAsync<IOException>(() =>
            _service.UploadAsync([file], "Request", 213, CancellationToken.None));
    }
}