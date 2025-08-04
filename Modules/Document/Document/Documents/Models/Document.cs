namespace Document.Documents.Models;

public class Document : Aggregate<long>
{
    public string RelateRequest { get; private set; } = default!;
    public long RelateId { get; private set; } = default!;
    public string DocType { get; private set; } = default!;
    public string Filename { get; private set; } = default!;
    public DateTime UploadTime { get; private set; } = default!;
    public string Prefix { get; private set; } = default!;
    public short Set { get; private set; } = default!;
    public string Comment { get; private set; } = default!;
    public string FilePath { get; private set; } = default!;

    private Document()
    {
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private Document(
        string relateRequest,
        long relateId,
        string docType,
        string filename,
        DateTime uploadTime,
        string prefix,
        short set,
        string comment,
        string filePath
        )
    {
        RelateRequest = relateRequest;
        RelateId = relateId;
        DocType = docType;
        Filename = filename;
        UploadTime = uploadTime;
        Prefix = prefix;
        Set = set;
        Comment = comment;
        FilePath = filePath;
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]

    public static Document Create(
        string RelateRequest,
        long RelateId,
        string docType,
        string filename,
        DateTime uploadTime,
        string prefix,
        short set,
        string comment,
        string filePath
    )
    {

        ArgumentNullException.ThrowIfNull(RelateRequest);
        ArgumentNullException.ThrowIfNull(docType);
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(comment);
        ArgumentNullException.ThrowIfNull(filePath);

        return new Document(
            RelateRequest,
            RelateId,
            docType,
            filename,
            uploadTime,
            prefix,
            set,
            comment,
            filePath);
    }

    public void UpdateComment(string newComment)
    {
        ArgumentNullException.ThrowIfNull(newComment);

        Comment = newComment;
    }

}