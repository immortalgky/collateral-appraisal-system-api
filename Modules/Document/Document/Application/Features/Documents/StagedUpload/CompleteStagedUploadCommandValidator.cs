namespace Document.Domain.Documents.Features.StagedUpload;

/// <summary>
/// The rules the single-request upload used to get from <c>UploadDocumentCommandValidator</c>,
/// which it no longer goes through. Every length here is a column width in
/// <c>document.Documents</c>: unchecked, an over-long value reaches SQL Server and comes back as a
/// 500 about truncation — after the whole file has been written and moved.
/// </summary>
public class CompleteStagedUploadCommandValidator : AbstractValidator<CompleteStagedUploadCommand>
{
    public CompleteStagedUploadCommandValidator()
    {
        RuleFor(x => x.Metadata.UploadSessionId)
            .NotEmpty()
            .WithMessage("Upload session ID is required");

        RuleFor(x => x.Metadata.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File cannot be empty");

        RuleFor(x => x.Metadata.FileName)
            .NotEmpty()
            .WithMessage("File name is required")
            .MaximumLength(255)
            .WithMessage("File name cannot exceed 255 characters");

        RuleFor(x => x.Metadata.ContentType)
            .NotEmpty()
            .WithMessage("Content type is required")
            .MaximumLength(100)
            .WithMessage("Content type cannot exceed 100 characters");

        RuleFor(x => x.Metadata.DocumentType)
            .NotEmpty()
            .WithMessage("documentType is required")
            .MaximumLength(10)
            .WithMessage("Document type cannot exceed 10 characters");

        RuleFor(x => x.Metadata.DocumentCategory)
            .MaximumLength(10)
            .WithMessage("Document category cannot exceed 10 characters");

        RuleFor(x => x.Metadata.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Metadata.Description))
            .WithMessage("Description cannot exceed 500 characters");
    }
}
