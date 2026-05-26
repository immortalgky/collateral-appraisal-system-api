namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateSupportingData;

public class UpdateSupportingDataCommandValidator
    : AbstractValidator<UpdateSupportingDataCommand>
{
    public UpdateSupportingDataCommandValidator()
    {
        RuleFor(x => x.Header).NotNull();
        RuleFor(x => x.Header.ImportChannel).NotEmpty();
        RuleFor(x => x.Header.SourceOfData).NotEmpty();
        RuleFor(x => x.Header.ImportDate).NotEmpty();
    }
}