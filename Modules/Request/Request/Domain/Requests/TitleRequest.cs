namespace Request.Domain.Requests;

/// <summary>
/// Legacy TitleDocument value object - not currently used.
/// Renamed to avoid conflict with TitleDocument aggregate in TitleDocuments module.
/// Can be deleted if confirmed unused.
/// </summary>
[Obsolete("This value object is not used. Consider deleting it.")]
public class TitleDocumentLegacy : ValueObject
{
    public TitleDocumentLegacy()
    {
    }

    private TitleDocumentLegacy
    (
        string docType,
        string fileName,
        DateTime uploadDate,
        string prefix,
        short set,
        string comment,
        string filePath
    )
    {
        DocType = docType;
        FileName = fileName;
        UploadDate = uploadDate;
        Prefix = prefix;
        Set = set;
        Comment = comment;
        FilePath = filePath;
    }

    public string DocType { get; }
    public string FileName { get; }
    public DateTime UploadDate { get; }
    public string Prefix { get; }
    public short Set { get; }
    public string Comment { get; }
    public string FilePath { get; }

    public static TitleDocumentLegacy Of
    (
        string docType,
        string fileName,
        DateTime uploadDate,
        string prefix,
        short set,
        string comment,
        string filePath
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(docType);
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        ArgumentException.ThrowIfNullOrEmpty(comment);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        return new TitleDocumentLegacy
        (
            docType,
            fileName,
            uploadDate,
            prefix,
            set,
            comment,
            filePath
        );
    }
}