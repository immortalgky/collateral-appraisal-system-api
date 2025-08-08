using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Document.Contracts.Documents.Dtos;
using Document.Documents.Features.DeleteDocument;
using Document.Documents.Features.UpdateDocument;
using Integration.Document.Integrations.Tests;
using Integration.Fixtures;
using Integration.Helpers;

namespace Integration.Document.Integrations.Tests;


public class UpdateDocumentByIdTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private readonly string relateRequest = "Request";
    private readonly long id = 1;
    private readonly short n = 1;
    private readonly string newComment = "Hello World!";

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

        var updateRequest = new UpdateDocumentRequest(id, newComment)
        {
            Id = id,
            NewComment = newComment
        };

        var updateContent = JsonContent.Create(updateRequest);
        var updateResponse = await _client.PutAsync($"/documents/{TestFileHelpers.CurrId}", updateContent, TestContext.Current.CancellationToken);
        updateResponse.EnsureSuccessStatusCode();
        var updateResponseBody = await updateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var updateResult = JsonSerializer.Deserialize<UpdateDocumentResponse>(updateResponseBody, JsonHelper.Options);

        Assert.NotNull(updateResult);
        Assert.True(updateResult.IsSuccess);

        var getResponse = await _client.GetAsync($"/documents/{TestFileHelpers.CurrId}", TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();
        var getResponseBody = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var getResult = JsonSerializer.Deserialize<DocumentDto>(getResponseBody, JsonHelper.Options);

        Assert.NotNull(getResult);
        Assert.NotNull(getResult.FilePath);
        Assert.Equal(id, getResult.RelateId);
        Assert.Equal(relateRequest, getResult.RelateRequest);
        Assert.Equal(newComment, getResult.Comment);
        Assert.False(string.IsNullOrWhiteSpace(getResult.Filename));

        var deleteResponse = await _client.DeleteAsync($"/documents/{TestFileHelpers.CurrId++}", TestContext.Current.CancellationToken);
        deleteResponse.EnsureSuccessStatusCode();
        var deleteResponseBody = await deleteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var deleteResult = JsonSerializer.Deserialize<DeleteDocumentResult>(deleteResponseBody, JsonHelper.Options);

        Assert.NotNull(deleteResult);
        Assert.True(deleteResult.IsSuccess);
        TestFileHelpers.CurrId++;
    }
}