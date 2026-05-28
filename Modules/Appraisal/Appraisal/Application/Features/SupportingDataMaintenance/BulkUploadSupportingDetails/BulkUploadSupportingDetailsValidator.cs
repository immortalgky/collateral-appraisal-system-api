using FluentValidation;

namespace Appraisal.Application.Features.SupportingDataMaintenance.BulkUploadSupportingDetails;

public class BulkUploadSupportingDetailsValidator
    : AbstractValidator<BulkUploadSupportingDetailsCommand>
{
    public BulkUploadSupportingDetailsValidator()
    {
        RuleFor(x => x.SupportingId)
            .NotEmpty()
            .WithMessage("SupportingId must not be empty.");

        RuleFor(x => x.FileStream)
            .NotNull()
            .WithMessage("A file stream is required.");
    }
}
