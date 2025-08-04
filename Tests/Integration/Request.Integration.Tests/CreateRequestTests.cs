using System.Text;
using System.Text.Json;
using Integration.Factories;
using Request.Requests.Features.CreateRequest;

namespace Integration.Request.Integration.Tests;

public class CreateRequestTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateRequest_ValidRequest_ReturnsSuccess()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Request.Integration.Tests", "TestData", "CreateRequest.json");
        var json = await File.ReadAllTextAsync(jsonPath, TestContext.Current.CancellationToken);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/requests", content, TestContext.Current.CancellationToken);
        
        var statusCodeException = Record.Exception(() => response.EnsureSuccessStatusCode());
        Assert.Null(statusCodeException);

        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var deserializeException = Record.Exception(() => JsonSerializer.Deserialize<CreateRequestResult>(responseBody));
        Assert.Null(deserializeException);
    }
}