namespace Document.Configurations;

/// <summary>
/// Limits for the chunked upload route, which is how files too large for one request reach the
/// system. Bound to <c>FileStorage:ChunkedUpload</c>; the defaults here are the shipped values, so
/// nothing has to be added to appsettings for this to work.
/// </summary>
public sealed class ChunkedUploadOptions
{
    public const string SectionName = "FileStorage:ChunkedUpload";

    /// <summary>
    /// The largest file that may be assembled from chunks. Deliberately separate from
    /// <c>FileStorage:MaxFileSizeBytes</c>, which stays where it is: that one caps a single
    /// request, and raising it would mean raising the IIS body limit on every node and letting a
    /// gigabyte spool through the system disk on its way in.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 1024L * 1024 * 1024;

    /// <summary>
    /// The largest body one chunk request may carry. The client picks its own chunk size under
    /// this; 8 MB is what the app sends, and the headroom here means a client that batches two
    /// together is not refused. Kept well below the 30,000,000 bytes IIS allows by default, so
    /// this route needs no server-side configuration to work at all — which is the point of it.
    /// </summary>
    public int MaxChunkSizeBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// How long an unfinished upload's staging folder is kept before the cleanup job removes it.
    /// An upload abandoned at 90% holds its bytes on the share until then.
    /// </summary>
    public int StagingRetentionHours { get; set; } = 24;
}
