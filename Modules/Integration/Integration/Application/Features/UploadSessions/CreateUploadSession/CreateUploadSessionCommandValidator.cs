using FluentValidation;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public class CreateUploadSessionCommandValidator : AbstractValidator<CreateUploadSessionCommand>
{
    public CreateUploadSessionCommandValidator()
    {
        // ClientReference / ExternalCaseKey have no DB length to mirror; these are boundary caps.
        RuleFor(x => x.ClientReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExternalCaseKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserAgent).MaximumLength(512);
        RuleFor(x => x.IpAddress).MaximumLength(45); // IPv6 max textual length
    }
}
