using System.Text.Json;
using Integration.Fixtures;
using Integration.Helpers;
using Integration.Request.Integration.Tests.Helpers;
using Request.Application.Features.Requests.DeleteRequest;

namespace Integration.Request.Integration.Tests;

public class DeleteRequestTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task DeleteRequest_ValidDeleteRequest_RequestDisappears()
    {
        // Create request first
        var createRequestResult = await RequestTestHelper.CreateRequest(_client);

        // Delete the request
        var deleteRequestResponse = await _client.DeleteAsync($"/requests/{createRequestResult.Id}", TestContext.Current.CancellationToken);

        var deleteStatusCodeException = Record.Exception(deleteRequestResponse.EnsureSuccessStatusCode);
        Assert.Null(deleteStatusCodeException);

        var deleteResponseContent = await deleteRequestResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var deleteRequestResult = JsonSerializer.Deserialize<DeleteRequestResult>(deleteResponseContent, JsonHelper.Options);
        Assert.NotNull(deleteRequestResult);
        Assert.True(deleteRequestResult.IsSuccess);

        // Get the deleted request by Id
        var getRequestByIdResponse = await _client.GetAsync($"/requests/{createRequestResult.Id}", TestContext.Current.CancellationToken);
        var getRequestByIdException = Record.Exception(getRequestByIdResponse.EnsureSuccessStatusCode);
        Assert.NotNull(getRequestByIdException);
    }
}