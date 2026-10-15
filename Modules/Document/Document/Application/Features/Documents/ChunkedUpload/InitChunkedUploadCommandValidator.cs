using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Document.Domain.Documents.Features.ChunkedUpload;

public class InitChunkedUploadCommandValidator : AbstractValidator<InitChunkedUploadCommand>
{
    public InitChunkedUploadCommandValidator(
        IOptions<FileStorageConfiguration> fileStorageOptions,
        IOptions<ChunkedUploadOptions> chunkedUploadOptions)
    {
        var allowedExtensions = fileStorageOptions.Value.AllowedExtensions;
        var maxFileSizeBytes = chunkedUploadOptions.Value.MaxFileSizeBytes;

        RuleFor(x => x.UploadSessionId)
            .NotEmpty()
            .WithMessage("Upload session ID is required");

        RuleFor(x => x.FileName)
            // Cascade.Stop, not a When on the extension rule: FluentValidation applies a When to
            // every rule in the chain it closes, which would switch off the NotEmpty as well and
            // let a nameless file through. Stopping at the first failure keeps
            // Path.GetExtension from being handed a null and turning a 400 into a 500.
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("File name is required")
            .Must(fileName => allowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
            .WithMessage($"File extension is not allowed. Allowed extensions: {string.Join(", ", allowedExtensions)}");

        // Checked here because this is the last moment it costs nothing. Left to the end, a blank
        // content type takes the whole file up first and then fails the document's constructor,
        // throwing away a gigabyte that had already arrived.
        RuleFor(x => x.ContentType)
            .NotEmpty()
            .WithMessage("Content type is required");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File cannot be empty")
            .LessThanOrEqualTo(maxFileSizeBytes)
            .WithMessage($"File size must not exceed {maxFileSizeBytes / (1024 * 1024)}MB");

        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .WithMessage("Document type is required")
            .MaximumLength(10)
            .WithMessage("Document type cannot exceed 10 characters");

        // The column is nvarchar(10). Left unchecked, a longer value reaches SQL Server and comes
        // back as a 500 about truncation rather than a 400 naming the field.
        RuleFor(x => x.DocumentCategory)
            .NotEmpty()
            .WithMessage("Document category is required")
            .MaximumLength(10)
            .WithMessage("Document category cannot exceed 10 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description cannot exceed 500 characters");
    }
}
