using Shared.Exceptions;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

/// <summary>The opt-in parts of GET /appraisals/{id}?include=...; the default response has none of them.</summary>
[Flags]
public enum AppraisalInclude
{
    None = 0,
    Request = 1,
    Documents = 2
}

public static class AppraisalIncludeParser
{
    /// <summary>
    /// Parses the comma-separated <c>include</c> query value (<c>request</c>, <c>documents</c>; any case, spaces and
    /// repeats allowed). Empty or missing means none. An unknown value is a 400 naming it.
    /// </summary>
    public static AppraisalInclude Parse(string? include)
    {
        var result = AppraisalInclude.None;
        if (string.IsNullOrWhiteSpace(include)) return result;

        foreach (var part in include.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("request", StringComparison.OrdinalIgnoreCase)) result |= AppraisalInclude.Request;
            else if (part.Equals("documents", StringComparison.OrdinalIgnoreCase)) result |= AppraisalInclude.Documents;
            else throw new BadRequestException($"Unknown include '{part}'. Expected 'request' and/or 'documents'.");
        }

        return result;
    }
}
