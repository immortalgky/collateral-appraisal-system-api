namespace Reporting.Contracts;

/// <summary>
/// A generated report file ready to stream via <c>Results.File</c>:
/// raw bytes, MIME content type, and a suggested (timestamped) download name.
/// </summary>
public sealed record ReportFile(byte[] Bytes, string ContentType, string FileName);
