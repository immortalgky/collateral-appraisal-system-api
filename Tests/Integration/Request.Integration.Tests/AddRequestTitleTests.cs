using System.Text;
using System.Text.Json;
using Integration.Fixtures;
using Integration.Helpers;
using Integration.Request.Integration.Tests.Helpers;

// using Request.RequestTitles.Features.CreateRequestTitle;
// using Request.RequestTitles.Features.GetRequestTitleById;

namespace Integration.Request.Integration.Tests;

public class AddRequestTitleTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task AddRequestTitle_ValidRequest_NewTitleAppears()
    {
        // // Create request first
        // var createRequestResult = await RequestTestHelper.CreateRequest(_client);
        //
        // // Create request title
        // var addRequestTitleJson = await JsonHelper.JsonFileToJson("Request.Integration.Tests", "AddRequestTitle_ValidTitle.json");
        // var addRequestTitleRequest = JsonSerializer.Deserialize<AddRequestTitlesRequest>(addRequestTitleJson, JsonHelper.Options)!;
        //
        // var addRequestTitleStringContent = new StringContent(addRequestTitleJson, Encoding.UTF8, "application/json");
        //
        // var addRequestTitleResponse = await _client.PostAsync($"/requests/{createRequestResult.Id}/titles", addRequestTitleStringContent, TestContext.Current.CancellationToken);
        // Assert.Null(Record.Exception(addRequestTitleResponse.EnsureSuccessStatusCode));
        //
        // var addRequestTitleResponseBody = await addRequestTitleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // var addRequestTitleResult = JsonSerializer.Deserialize<AddRequestTitlesResult>(addRequestTitleResponseBody, JsonHelper.Options);
        // Assert.NotNull(addRequestTitleResult);
        //
        // // Get request title
        // var getRequestTitleResponse = await _client.GetAsync($"/requests/{createRequestResult.Id}/titles/{addRequestTitleResult.Id}", TestContext.Current.CancellationToken);
        //
        // Assert.Null(Record.Exception(getRequestTitleResponse.EnsureSuccessStatusCode));
        //
        // var getRequestTitleResponseContent = await getRequestTitleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // var getRequestTitleResult = JsonSerializer.Deserialize<GetRequestTitleByIdResult>(getRequestTitleResponseContent, JsonHelper.Options);
        // Assert.NotNull(getRequestTitleResult);
        // Assert.Equivalent(addRequestTitleRequest.CollateralType, getRequestTitleResult.CollateralType);
    }
}