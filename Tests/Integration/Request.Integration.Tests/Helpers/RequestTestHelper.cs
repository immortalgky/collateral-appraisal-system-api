using System.Text.Json;
using Integration.Helpers;
using Request.Application.Features.Requests.CreateRequest;

namespace Integration.Request.Integration.Tests.Helpers;

internal static class RequestTestHelper
{
    internal async static Task<CreateRequestResult> CreateRequest(HttpClient client)
    {
        var content = await JsonHelper.JsonToStringContent("Request.Integration.Tests", "CreateRequest_ValidRequest.json");
        var response = await client.PostAsync("/requests", content, TestContext.Current.CancellationToken);

        var statusCodeException = Record.Exception(response.EnsureSuccessStatusCode);
        Assert.Null(statusCodeException);

        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<CreateRequestResult>(responseBody, JsonHelper.Options);
        Assert.NotNull(result);

        return result;
    }
}