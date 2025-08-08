using System.Net;
using System.Text.Json;
using Document.Contracts.Documents.Dtos;
using Document.Documents.Features.DeleteDocument;
using Integration.Document.Integrations.Tests;
using Integration.Fixtures;
using Integration.Helpers;

namespace Integration.Document.Integrations.Tests;


public class GetDocumentByIdTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private readonly string relateRequest = "Request";
    private readonly long id = 1;

    [Fact]
    public async Task GetDocumentById_ValidId_ReturnsDocument()
    {
        using var multipartContent = TestFileHelpers.CreateHttp(1);

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
        Assert.Single(getsResult);

        var getResponse = await _client.GetAsync($"/documents/{getsResult[0].Id}", TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();
        var getResponseBody = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var getResult = JsonSerializer.Deserialize<DocumentDto>(getResponseBody, JsonHelper.Options);

        Assert.NotNull(getResult);
        Assert.NotNull(getResult.FilePath);
        Assert.Equal(id, getResult.RelateId);
        Assert.Equal(relateRequest, getResult.RelateRequest);
        Assert.False(string.IsNullOrWhiteSpace(getResult.Filename));

        var deleteResponse = await _client.DeleteAsync($"/documents/{getResult.Id}", TestContext.Current.CancellationToken);
        deleteResponse.EnsureSuccessStatusCode();
        var deleteResponseBody = await deleteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var deleteResult = JsonSerializer.Deserialize<DeleteDocumentResult>(deleteResponseBody, JsonHelper.Options);

        Assert.NotNull(deleteResult);
        Assert.True(deleteResult.IsSuccess);
    }
}