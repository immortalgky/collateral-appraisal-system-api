using System.Net.Http.Json;

namespace Integration.Document.Integrations.Tests;

public class DocumentModuleIntegrationTest(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task DocumentModule()
    {
        using var multipartContent = TestFileHelpers.CreateHttp(5);

        var uploadResponse = await _client.PostAsync($"/documents/{"Request"}/{1}", multipartContent, TestContext.Current.CancellationToken);
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
        Assert.Equal(5, getsResult.Count);

        var getResponse = await _client.GetAsync($"/documents/{getsResult[4].Id}", TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();
        var getResponseBody = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var getResult = JsonSerializer.Deserialize<DocumentDto>(getResponseBody, JsonHelper.Options);

        Assert.NotNull(getResult);
        Assert.NotNull(getResult.FilePath);
        Assert.Equal(1, getResult.RelateId);
        Assert.Equal("Request", getResult.RelateRequest);
        Assert.False(string.IsNullOrWhiteSpace(getResult.Filename));

        var updateRequest = new UpdateDocumentRequest(4, "This is New Comment")
        {
            Id = 4,
            NewComment = "This is New Comment"
        };

        var updateContent = JsonContent.Create(updateRequest);
        var updateResponse = await _client.PutAsync($"/documents/{4}", updateContent, TestContext.Current.CancellationToken);
        updateResponse.EnsureSuccessStatusCode();
        var updateResponseBody = await updateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var updateResult = JsonSerializer.Deserialize<UpdateDocumentResponse>(updateResponseBody, JsonHelper.Options);

        Assert.NotNull(updateResult);
        Assert.True(updateResult.IsSuccess);

        var deletesResult = new List<DeleteDocumentResult>();
        foreach (var file in getsResult)
        {
            var deleteResponse = await _client.DeleteAsync($"/documents/{file.Id}", TestContext.Current.CancellationToken);
            deleteResponse.EnsureSuccessStatusCode();
            var deleteResponseBody = await deleteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var deleteResult = JsonSerializer.Deserialize<DeleteDocumentResult>(deleteResponseBody, JsonHelper.Options);
            Assert.NotNull(deleteResult);

            deletesResult.Add(deleteResult); 
        }
        Assert.NotNull(deletesResult);
        Assert.True(deletesResult.All(r => r.IsSuccess));
    }
}