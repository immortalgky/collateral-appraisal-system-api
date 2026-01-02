using System.Text;
using System.Text.Json;

namespace Integration.Helpers;

internal static class JsonHelper
{
    internal async static Task<StringContent> JsonToStringContent(string folderName, string fileName)
    {
        var json = await JsonFileToJson(folderName, fileName);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return content;
    }

    internal async static Task<string> JsonFileToJson(string folderName, string fileName)
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, folderName, "TestData", fileName);
        var json = await File.ReadAllTextAsync(jsonPath, TestContext.Current.CancellationToken);
        return json;
    }

    internal static JsonSerializerOptions Options { get; } = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
}