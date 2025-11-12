using System;

namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleValidator : AbstractValidator<DraftRequestTitleCommand>
{
    public DraftRequestTitleValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("ReqeustId is required");

        RuleFor(x => x.CollateralType)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        // RuleFor(x => x.CollateralStatus);

        RuleFor(x => x.TitleNo)
            .MaximumLength(200)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.DeedType)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.TitleDetail)
            .MaximumLength(200)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.Rawang)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.LandNo)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.SurveyNo)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");



        RuleFor(x => x.TitleDetail)
            .MaximumLength(200);

        RuleFor(x => x.AreaRai)
            .GreaterThanOrEqualTo(0).WithMessage("Rai must be greater than or equal to 0.")
            .When(x => x.AreaRai.HasValue);

        RuleFor(x => x.AreaNgan)
            .GreaterThanOrEqualTo(0).WithMessage("Ngan must be greater than or equal to 0.")
            .When(x => x.AreaNgan.HasValue);
        
        RuleFor(x => x.AreaSquareWa)
            .PrecisionScale(19, 2, true);

        RuleFor(x => x.UsableArea)
            .GreaterThan(0).WithMessage("UsageArea must be greater than 0.")
            .When(x => x.UsableArea.HasValue);

        RuleFor(x => x.NoOfBuilding)
            .GreaterThan(0).WithMessage("NoOfBuilding must be greater than 0.")
            .When(x => x.NoOfBuilding.HasValue);

        RuleFor(x => x.NumberOfMachinery)
            .GreaterThan(0).WithMessage("NoOfMachine must be greater than 0.")
            .When(x => x.NumberOfMachinery.HasValue);

        RuleFor(x => x.TitleAddress)
            .SetValidator(new TitleDocDtoValidator());

        RuleFor(x => x.DopaAddress)
            .SetValidator(new DopaAddressDtoValidator());
    }
}
