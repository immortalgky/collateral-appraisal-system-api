namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateSupportingData;

public class CreateSupportingDataCommandValidator
    : AbstractValidator<CreateSupportingDataCommand>
{
    public CreateSupportingDataCommandValidator()
    {
        RuleFor(x => x.Header).NotNull();
        RuleFor(x => x.Header.ImportChannel).NotEmpty();
        RuleFor(x => x.Header.SourceOfData).NotEmpty();
        RuleFor(x => x.Header.ImportDate).NotEmpty();
    }
}