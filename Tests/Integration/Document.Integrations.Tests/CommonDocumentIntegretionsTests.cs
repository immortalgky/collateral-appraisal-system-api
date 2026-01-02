using MassTransit.NewIdProviders;
using Microsoft.AspNetCore.Http;

namespace Integration.Document.Integrations.Tests;

public static class TestFileHelpers
{
    public static long CurrId = 1;
    public static IFormFile CreateMockFile(string fileName, byte[] content, string contentType = "application/pdf")
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public static byte[] GenerateBytes(int sizeInBytes)
    {
        return Enumerable.Repeat((byte)1, sizeInBytes).ToArray();
    }

    public static MultipartContent CreateHttp(short n)
    {
        var files = Enumerable.Range(1, n)
            .Select(i => CreateMockFile($"file_{i}.pdf", GenerateBytes(1 * 1024 * 1024 + i)))
            .ToList();

        var multipartContent = new MultipartFormDataContent();

        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.OpenReadStream());
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            multipartContent.Add(streamContent, "Files", file.FileName);
        }

        return multipartContent;
    }
}