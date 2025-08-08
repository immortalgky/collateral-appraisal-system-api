using System.Net;
using System.Text.Json;
using Document.Contracts.Documents.Dtos;
using Document.Documents.Features.DeleteDocument;
using Integration.Document.Integrations.Tests;
using Integration.Fixtures;
using Integration.Helpers;

namespace Integration.Document.Integrations.Tests;


public class UploadDocumentByIdTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private readonly string relateRequest = "Request";
    private readonly long id = 1;
    private readonly short n = 5;

    [Fact]
    public async Task UploadDocument_ValidId()
    {
        using var multipartContent = TestFileHelpers.CreateHttp(n);

        var uploadResponse = await _client.PostAsync($"/documents/{relateRequest}/{id}", multipartContent, TestContext.Current.CancellationToken);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadResponseBody = await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var uploadResult = JsonSerializer.Deserialize<List<UploadResultDto>>(uploadResponseBody, JsonHelper.Options);

        Assert.NotNull(uploadResult);
        Assert.True(uploadResult.All(r => r.IsSuccess));
        Assert.True(uploadResult.All(r => r.Comment == "Success"));

        var getsResponse = await _client.GetAsync("/documents", TestContext.Current.CancellationToken);
        getsResponse.EnsureSuccessStatusCode();
        var getsResponseBody = await getsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var getsResult = JsonSerializer.Deserialize<List<DocumentDto>>(getsResponseBody, JsonHelper.Options);

        Assert.NotNull(getsResult);
        Assert.All(getsResult, r => Assert.False(string.IsNullOrWhiteSpace(r.FilePath)));
        Assert.Equal(n, getsResult.Count);

        var deletesResult = new List<DeleteDocumentResult>();
        foreach (var file in getsResult)
        {
            var deleteResponse = await _client.DeleteAsync($"/documents/{file.Id}", TestContext.Current.CancellationToken);
            deleteResponse.EnsureSuccessStatusCode();
            var deleteResponseBody = await deleteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var deleteResult = JsonSerializer.Deserialize<DeleteDocumentResult>(deleteResponseBody, JsonHelper.Options);
            Assert.NotNull(deleteResult);

            deletesResult.Add(deleteResult);
            TestFileHelpers.CurrId++;
        }
        Assert.NotNull(deletesResult);
        Assert.True(deletesResult.All(r => r.IsSuccess));
    }
}