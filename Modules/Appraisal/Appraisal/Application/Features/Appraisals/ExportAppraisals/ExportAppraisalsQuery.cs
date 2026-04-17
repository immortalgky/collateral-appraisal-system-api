using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.ExportAppraisals;

/// <summary>
/// Query to export all matching appraisals as a file download (no pagination, max 10,000 rows).
/// </summary>
public record ExportAppraisalsQuery(
    GetAppraisalsFilterRequest? Filter,
    string Format // "xlsx" (default) or "csv"
) : IQuery<ExportAppraisalsResult>;

/// <summary>
/// Result carrying the raw file bytes, MIME type, and suggested file name.
/// </summary>
public record ExportAppraisalsResult(
    byte[] FileBytes,
    string ContentType,
    string FileName
);
