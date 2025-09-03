using System.Text;
using System.Text.Json;

namespace Integration.Helpers;

internal static class TestCaseHelper
{
    internal static async Task<CreateResult> TestCreateEndpoint<CreateResult>(
        string folderName,
        string fileName,
        HttpClient client,
        string url
    )
    {
        // Create new item
        var createJson = await JsonHelper.JsonFileToJson(folderName, fileName);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await client.PostAsync(
            url,
            createContent,
            TestContext.Current.CancellationToken
        );

        var statusCodeException = Record.Exception(createResponse.EnsureSuccessStatusCode);
        Assert.Null(statusCodeException);

        var createResponseBody = await createResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        var createResult = JsonSerializer.Deserialize<CreateResult>(
            createResponseBody,
            JsonHelper.Options
        );
        Assert.NotNull(createResult);
        return createResult;
    }

    internal static async Task<GetByIdResult> TestGetByIdEndpoint<GetByIdResult>(
        long id,
        HttpClient client,
        string url
    )
    {
        // Get the item by id
        var getByIdResponse = await client.GetAsync(
            $"{url}/{id}",
            TestContext.Current.CancellationToken
        );

        var getStatusCodeException = Record.Exception(getByIdResponse.EnsureSuccessStatusCode);
        Assert.Null(getStatusCodeException);

        var getByIdResponseContent = await getByIdResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        var getByIdResult = JsonSerializer.Deserialize<GetByIdResult>(
            getByIdResponseContent,
            JsonHelper.Options
        );
        Assert.NotNull(getByIdResult);
        return getByIdResult;
    }

    internal static async Task<DeleteResult> TestDeleteEndpoint<DeleteResult>(
        long id,
        HttpClient client,
        string url
    )
    {
        // Delete the item
        var deleteResponse = await client.DeleteAsync(
            $"{url}/{id}",
            TestContext.Current.CancellationToken
        );

        var deleteStatusCodeException = Record.Exception(deleteResponse.EnsureSuccessStatusCode);
        Assert.Null(deleteStatusCodeException);

        var deleteResponseContent = await deleteResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        var deleteResult = JsonSerializer.Deserialize<DeleteResult>(
            deleteResponseContent,
            JsonHelper.Options
        );
        Assert.NotNull(deleteResult);
        return deleteResult;
    }

    internal static async Task<UpdateResult> TestUpdateEndpoint<UpdateResult>(
        string folderName,
        string fileName,
        long id,
        HttpClient client,
        string url
    )
    {
        static string jsonTransformFunc(string j) => j;
        return await TestUpdateEndpoint<UpdateResult>(
            folderName,
            fileName,
            id,
            client,
            url,
            jsonTransformFunc
        );
    }

    internal static async Task<UpdateResult> TestUpdateEndpoint<UpdateResult>(
        string folderName,
        string fileName,
        long id,
        HttpClient client,
        string url,
        Func<string, string> jsonTransformFunc
    )
    {
        var updateJson = await JsonHelper.JsonFileToJson(folderName, fileName);
        var transformJson = jsonTransformFunc(updateJson);
        var updateContent = new StringContent(transformJson, Encoding.UTF8, "application/json");
        var updateResponse = await client.PatchAsync(
            $"{url}/{id}",
            updateContent,
            TestContext.Current.CancellationToken
        );

        var statusCodeException = Record.Exception(updateResponse.EnsureSuccessStatusCode);
        Assert.Null(statusCodeException);

        var updateResponseBody = await updateResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        var updateResult = JsonSerializer.Deserialize<UpdateResult>(
            updateResponseBody,
            JsonHelper.Options
        );
        Assert.NotNull(updateResult);

        return updateResult;
    }
}
