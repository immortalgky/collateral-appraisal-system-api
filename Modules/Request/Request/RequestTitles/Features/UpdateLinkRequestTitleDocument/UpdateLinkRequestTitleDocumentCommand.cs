namespace Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;

public record UpdateLinkRequestTitleDocumentCommand : ICommand<UpdateLinkRequestTitleDocumentResult>
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public Guid TitleId { get; init; }
    public Guid? DocumentId { get; init; }
    public string? DocumentType { get; init; }
    public string? Filename { get; init; }
    public string? Prefix { get; init; }
    public int Set { get; init; }
    public string? DocumentDescription { get; init; }
    public string? FilePath { get; init; }
    public string? CreatedWorkstation { get; init; }
    public bool IsRequired { get; init; }
    public string? UploadedBy { get; init; }
    public string? UploadedByName { get; init; }
}