using System.Text;
using Document.Data.Repository;
using Document.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Document.Tests.TestData;

namespace Document.Tests.Document.Documents.ServiceTests;

public class DocumentServiceTests
{
    private readonly IDocumentRepository _repo = Substitute.For<IDocumentRepository>();
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        _service = new DocumentService(_repo);
    }

    private static IFormFile CreateMockFile(string fileName, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }
    [Fact]
    public async Task UploadValidpdfFileUnder5Mb()
    {
        var file = CreateMockFile("file1.pdf", "Test PDF content");

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 100, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }

    [Fact]
    public async Task UploadValidPDFFileUnder5Mb()
    {
        var file = CreateMockFile("file1.PDF", "Test PDF content");

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(new List<IFormFile> { file }, "Request", 101, CancellationToken.None);

        Assert.Single(result.Result);
        Assert.True(result.Result[0].IsSuccess);
    }


    [Fact]
    public async Task Uploads5ValidFileUnder5Mb()
    {
        var files = Enumerable.Range(1, 5)
            .Select(i => CreateMockFile($"file_{i}.pdf", $"Test pdf file{i}"))
            .ToList();

        _repo.GetDocument(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _repo.UploadDocument(Arg.Any<global::Document.Documents.Models.Document>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _service.UploadAsync(files, "Request", 102, CancellationToken.None);

        Assert.Equal(5, result.Result.Count);
        Assert.All(result.Result, r => Assert.True(r.IsSuccess));
    }
}