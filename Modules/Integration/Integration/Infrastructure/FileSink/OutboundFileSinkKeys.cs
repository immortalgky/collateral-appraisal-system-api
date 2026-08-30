namespace Integration.Infrastructure.FileSink;

/// <summary>
/// DI keys for outbound sinks registered <i>in addition</i> to the default one.
///
/// The default <c>IOutboundFileSink</c> is chosen once at startup from
/// <c>FileTransfer:Outbound:FileSource</c> and is what almost every job should use. A job asks for a
/// key here only when a particular file must take a different transport regardless of that setting.
/// </summary>
public static class OutboundFileSinkKeys
{
    /// <summary>
    /// The plain filesystem adapter — a local folder or a UNC share the process account can reach —
    /// even in environments where the default sink is SFTP.
    ///
    /// Used by the regulatory Excel companion, which goes to a Windows share the Risk team reads
    /// rather than to AS400's SFTP drop. A destination row can say <i>where</i> a file goes but not
    /// <i>how</i> it gets there; handing a UNC path to the SFTP sink would have it try to create
    /// that path on the remote host.
    /// </summary>
    public const string FileSystem = "FileSystem";
}
