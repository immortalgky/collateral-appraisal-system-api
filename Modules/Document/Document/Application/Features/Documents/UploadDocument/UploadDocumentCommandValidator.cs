using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Document.Domain.Documents.Features.UploadDocument;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private readonly FileStorageConfiguration _fileStorageConfig;

    public UploadDocumentCommandValidator(IOptions<FileStorageConfiguration> fileStorageOptions)
    {
        _fileStorageConfig = fileStorageOptions.Value;

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required");

        RuleFor(x => x.File.Length)
            .GreaterThan(0)
            .WithMessage("File cannot be empty")
            .LessThanOrEqualTo(_fileStorageConfig.MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {_fileStorageConfig.MaxFileSizeBytes / 1_000_000}MB")
            .When(x => x.File != null);

        RuleFor(x => x.File.FileName)
            .Must(HasValidExtension)
            .WithMessage(
                $"File extension is not allowed. Allowed extensions: {string.Join(", ", _fileStorageConfig.AllowedExtensions)}")
            .When(x => x.File != null);

        RuleFor(x => x.UploadSessionId)
            .NotEmpty()
            .WithMessage("Upload session ID is required");

        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .WithMessage("Document type is required")
            .MaximumLength(50)
            .WithMessage("Document type cannot exceed 50 characters");

        // RuleFor(x => x.DocumentCategory)
        //     .NotEmpty()
        //     .WithMessage("Document category is required")
        //     .MaximumLength(50)
        //     .WithMessage("Document category cannot exceed 50 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description cannot exceed 500 characters");
    }

    private bool HasValidExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return _fileStorageConfig.AllowedExtensions.Contains(extension);
    }
}