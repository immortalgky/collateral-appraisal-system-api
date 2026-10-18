using System.Text.Json;
using Appraisal.Application.Features.Appraisals.GetAppraisalById;
using Appraisal.Application.Features.Appraisals.GetAppraisalRequest;
using Request.Contracts.Requests.Dtos;

namespace Appraisal.Tests.Application.Features.GetAppraisalById;

/// <summary>The body: the plain header with no include; each asked-for part is its own key, an explicit null / [] when missing.</summary>
public class AppraisalByIdResponseWriterTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static GetAppraisalByIdResult Header() =>
        new() { Id = Guid.NewGuid(), Status = "Completed", AppraisalType = "New", Priority = "Normal" };

    private static JsonElement Body(GetAppraisalByIdResult result, AppraisalInclude include) =>
        JsonSerializer.SerializeToElement(AppraisalByIdResponseWriter.Build(result, include, Web), Web);

    [Fact]
    public void Without_an_include_the_body_is_the_header_alone()
    {
        var body = Body(Header(), AppraisalInclude.None);

        Assert.Equal("Completed", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("request", out _));
        Assert.False(body.TryGetProperty("documents", out _));
    }

    [Fact]
    public void A_missing_request_is_an_explicit_null_when_it_was_asked_for_and_documents_stay_absent()
    {
        var body = Body(Header(), AppraisalInclude.Request); // Request left null: the part could not be read

        Assert.True(body.TryGetProperty("request", out var request));
        Assert.Equal(JsonValueKind.Null, request.ValueKind);
        Assert.False(body.TryGetProperty("documents", out _));
    }

    [Fact]
    public void Missing_documents_are_an_empty_array_when_asked_for_and_request_stays_absent()
    {
        var body = Body(Header(), AppraisalInclude.Documents);

        Assert.Equal(JsonValueKind.Array, body.GetProperty("documents").ValueKind);
        Assert.Equal(0, body.GetProperty("documents").GetArrayLength());
        Assert.False(body.TryGetProperty("request", out _));
    }

    [Fact]
    public void Both_parts_are_written_with_their_content()
    {
        var result = Header();
        result.Request = new AppraisalRequestDto(null, new RequestDetailCopyDto(false, null, null, null), [], [], []);
        result.Documents = [new AppraisalDocumentDto(Guid.NewGuid(), "D043", "D036", "Request", null, null, null, "a.pdf", null, null, 1, null, null, null, null, true)];

        var body = Body(result, AppraisalInclude.Request | AppraisalInclude.Documents);

        Assert.Equal(JsonValueKind.Null, body.GetProperty("request").GetProperty("prevAppraisal").ValueKind);
        Assert.Equal("D036", body.GetProperty("documents")[0].GetProperty("suggestedType").GetString());
    }
}
