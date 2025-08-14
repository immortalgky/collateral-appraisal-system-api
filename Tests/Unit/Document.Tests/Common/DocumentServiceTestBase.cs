namespace Document.Tests.Common;

public abstract class DocumentServiceTestBase
{
    protected readonly IDocumentRepository _repo;
    protected readonly DocumentService _service;

    protected DocumentServiceTestBase()
    {
        _repo = Substitute.For<IDocumentRepository>();
        _service = new DocumentService(_repo);
    }

    protected static IFormFile CreateMockFile(string fileName, byte[] content, string contentType = "application/pdf")
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    protected static byte[] GenerateBytes(int sizeInBytes)
    {
        return Enumerable.Repeat((byte)1, sizeInBytes).ToArray();
    }
}