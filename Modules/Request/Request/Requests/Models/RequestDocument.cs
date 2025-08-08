namespace Request.Requests.Models;

public class RequestDocument : Entity<long>
{
    public RequestDocument()
    {
    }

    private RequestDocument
    (
        string docType,
        string fileName,
        DateTime? uploadDate,
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

    public string DocType { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public DateTime? UploadDate { get; private set; } = default!;
    public string Prefix { get; private set; } = default!;
    public short Set { get; private set; } = default!;
    public string Comment { get; private set; } = default!;
    public string FilePath { get; private set; } = default!;

    public static RequestDocument Of(
        string docType,
        string fileName,
        DateTime? uploadDate,
        string prefix,
        short set,
        string comment,
        string filePath
    )
    {
        ArgumentNullException.ThrowIfNull(docType);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(comment);
        ArgumentNullException.ThrowIfNull(filePath);

        return new RequestDocument(
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