using FluentValidation;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public class CreateUploadSessionCommandValidator : AbstractValidator<CreateUploadSessionCommand>
{
    public CreateUploadSessionCommandValidator()
    {
        RuleFor(x => x.UserAgent).MaximumLength(512);
        RuleFor(x => x.IpAddress).MaximumLength(45); // IPv6 max textual length
    }
}
