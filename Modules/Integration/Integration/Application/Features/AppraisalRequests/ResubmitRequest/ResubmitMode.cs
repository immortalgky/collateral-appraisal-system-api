namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;

/// <summary>
/// Resubmit discriminator. The wire field is a nullable string; the handler parses it
/// case-insensitively. Null/whitespace defaults to DataFix for back-compat with existing
/// bank callers that don't know about the field.
/// </summary>
internal enum ResubmitMode
{
    DataFix,
    Followup
}
