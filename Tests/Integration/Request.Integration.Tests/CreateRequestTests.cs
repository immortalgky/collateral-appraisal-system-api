using System.Text;
using System.Text.Json;
using Integration.Fixtures;
using Integration.Helpers;
using Request.Application.Features.Requests.CreateRequest;
using Request.Application.Features.Requests.GetRequestById;

namespace Integration.Request.Integration.Tests;

public class CreateRequestTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task CreateRequest_ValidRequest_NewRequestAppears()
    {
        // Create new request
        var createRequestJson =
            await JsonHelper.JsonFileToJson("Request.Integration.Tests", "CreateRequest_ValidRequest.json");
        var createRequestContent = new StringContent(createRequestJson, Encoding.UTF8, "application/json");
        var createRequestResponse =
            await _client.PostAsync("/requests", createRequestContent, TestContext.Current.CancellationToken);

        var statusCodeException = Record.Exception(createRequestResponse.EnsureSuccessStatusCode);
        Assert.Null(statusCodeException);

        var createRequestResponseBody =
            await createRequestResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var createRequestResult =
            JsonSerializer.Deserialize<CreateRequestResult>(createRequestResponseBody, JsonHelper.Options);
        Assert.NotNull(createRequestResult);

        // Get the request by Id
        var getRequestByIdResponse =
            await _client.GetAsync($"/requests/{createRequestResult.Id}", TestContext.Current.CancellationToken);

        var getRequestStatusCodeException = Record.Exception(getRequestByIdResponse.EnsureSuccessStatusCode);
        Assert.Null(getRequestStatusCodeException);

        var getRequestByIdResponseContent =
            await getRequestByIdResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var getRequestByIdResult =
            JsonSerializer.Deserialize<GetRequestByIdResult>(getRequestByIdResponseContent, JsonHelper.Options);
        Assert.NotNull(getRequestByIdResult);
        Assert.Equivalent(getRequestByIdResult.Id, createRequestResult.Id);

        // Compare the result with the original one
        var createRequestRequest =
            JsonSerializer.Deserialize<CreateRequestRequest>(createRequestJson, JsonHelper.Options);
        Assert.Equivalent(createRequestRequest!.Purpose, getRequestByIdResult.Purpose);
    }
}