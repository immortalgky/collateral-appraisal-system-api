using System.Text.Json;
using Mapster;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

/// <summary>
/// Shapes the GET /appraisals/{id} body for both routes (Appraisal and Integration). Without an include it is the
/// plain header response, exactly as before. With one, each asked-for part is a key of its own: a part that is
/// missing is an explicit <c>null</c> (<c>request</c>) or <c>[]</c> (<c>documents</c>) rather than an omitted key, and a
/// part that was not asked for is absent. The names follow the application's JSON options.
/// </summary>
public static class AppraisalByIdResponseWriter
{
    public static object Build(GetAppraisalByIdResult result, AppraisalInclude include, JsonSerializerOptions options)
    {
        var header = result.Adapt<GetAppraisalByIdResponse>();
        if (include == AppraisalInclude.None)
            return header;

        var body = JsonSerializer.SerializeToNode(header, options)!.AsObject();

        if (include.HasFlag(AppraisalInclude.Request))
            body[Name("Request", options)] = result.Request is null ? null : JsonSerializer.SerializeToNode(result.Request, options);

        if (include.HasFlag(AppraisalInclude.Documents))
            body[Name("Documents", options)] = JsonSerializer.SerializeToNode(result.Documents ?? [], options);

        return body;
    }

    private static string Name(string property, JsonSerializerOptions options) =>
        options.PropertyNamingPolicy?.ConvertName(property) ?? property;
}
