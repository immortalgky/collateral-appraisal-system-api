using System.Text;
using System.Text.Json;
using Integration.Fixtures;
using Integration.Helpers;
using Integration.Request.Integration.Tests.Helpers;
using Request.Application.Features.RequestComments.AddRequestComment;
using Request.Application.Features.RequestComments.GetRequestCommentsByRequestId;

namespace Integration.Request.Integration.Tests;

public class AddRequestCommentTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task AddRequestComment_ValidRequest_NewCommentAppears()
    {
        // Create request first
        var createRequestResult = await RequestTestHelper.CreateRequest(_client);

        // Create request comment
        var addRequestCommentJson = await JsonHelper.JsonFileToJson("Request.Integration.Tests", "AddRequestComment_ValidComment.json");
        var addRequestCommentRequest = JsonSerializer.Deserialize<AddRequestCommentRequest>(addRequestCommentJson, JsonHelper.Options)!;
        var addRequestCommentStringContent = new StringContent(addRequestCommentJson, Encoding.UTF8, "application/json");

        var addRequestCommentResponse = await _client.PostAsync($"/requests/{createRequestResult.Id}/comments", addRequestCommentStringContent, TestContext.Current.CancellationToken);
        Assert.Null(Record.Exception(addRequestCommentResponse.EnsureSuccessStatusCode));

        var addRequestCommentResponseBody = await addRequestCommentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var addRequestCommentResult = JsonSerializer.Deserialize<AddRequestCommentResult>(addRequestCommentResponseBody, JsonHelper.Options);
        Assert.NotNull(addRequestCommentResult);
        Assert.True(addRequestCommentResult.IsSuccess);

        // Get request comment
        var getRequestCommentResponse = await _client.GetAsync($"/requests/{createRequestResult.Id}/comments", TestContext.Current.CancellationToken);

        Assert.Null(Record.Exception(getRequestCommentResponse.EnsureSuccessStatusCode));

        var getRequestCommentResponseContent = await getRequestCommentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var getRequestCommentResult = JsonSerializer.Deserialize<GetRequestCommentsByRequestIdResult>(getRequestCommentResponseContent, JsonHelper.Options);
        Assert.NotNull(getRequestCommentResult);
        Assert.Contains(getRequestCommentResult.Comments, c => c.Comment.Equals(addRequestCommentRequest.Comment));
    }
}