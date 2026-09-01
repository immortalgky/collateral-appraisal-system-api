namespace Integration.Infrastructure.FileInterface;

/// <summary>
/// Builds the name of an outbound interface file from the naming columns on an
/// <c>integration.FileInterfaceConfigs</c> row.
///
/// <b>A blank value means "leave this part out".</b> Both export jobs used to interpolate
/// <c>$"{prefix}{now.ToString(dateFormat)}.{ext}"</c> directly, which made a name without a date
/// impossible to express: <c>FileNameDateFormat</c> is only replaced by the job's default when it is
/// NULL, so an empty string reached <see cref="DateTime.ToString(string)"/> — and .NET reads an empty
/// format as the standard "G" specifier, producing <c>6/30/2026 2:00:00 AM</c>. Slashes and colons
/// are not legal in a file name, so the run failed rather than writing an undated file.
///
/// The bank's regulatory feed is collected as <c>RDTCLSINT4.txt</c>, one fixed name overwritten every
/// month, so "no date" has to be a configurable outcome. An empty <c>FileExtension</c> is honoured
/// the same way for symmetry — nothing needs it today, but a trailing bare dot would be the only
/// other way to spell it.
///
/// NULL still means "the caller's default"; the jobs pass their own, so their behaviour is unchanged.
/// </summary>
public static class OutboundFileName
{
    public static string Build(string? prefix, string? dateFormat, string? extension, DateTime now)
    {
        var name = prefix ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(dateFormat))
            name += now.ToString(dateFormat);

        if (!string.IsNullOrWhiteSpace(extension))
            name += $".{extension.TrimStart('.')}";

        return name;
    }
}
