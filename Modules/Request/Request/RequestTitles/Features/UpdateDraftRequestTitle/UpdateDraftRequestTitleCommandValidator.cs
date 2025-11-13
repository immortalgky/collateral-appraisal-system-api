namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestTitleCommandValidator : AbstractValidator<UpdateDraftRequestTitleCommand>
{
    public UpdateDraftRequestTitleCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("ReqeustId is required");

        RuleFor(x => x.CollateralType)
            .NotEmpty()
            .WithMessage("CollateralType is required.")
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.CollateralStatus)
            .NotEmpty();

        // == TitleDeedInfoDto ==
        RuleFor(x => x.TitleDeedInfoDto.TitleNo)
            .MaximumLength(200)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.TitleDeedInfoDto.DeedType)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.TitleDeedInfoDto.TitleDetail)
            .MaximumLength(200)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.TitleDeedInfoDto.TitleDetail)
            .MaximumLength(200);

        // == SurveyInfoDto ==
        RuleFor(x => x.SurveyInfoDto.Rawang)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.SurveyInfoDto.LandNo)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.SurveyInfoDto.SurveyNo)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        // == LandAreaDto ==
        RuleFor(x => x.LandAreaDto.AreaSquareWa)
            .PrecisionScale(5, 2, true)
            .WithMessage("'{PropertyName}' must be a number with up to 5 total digits and up to 2 decimal places.");

        // == OwnerName ==
        RuleFor(x => x.OwnerName)
            .MaximumLength(500)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.RegistrationNumber)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        // == MachineryDto ==
        RuleFor(x => x.MachineryDto.MachineryType)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.MachineryDto.InstallationStatus)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.MachineryDto.InvoiceNumber)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");        
        
        // == VehicleDto ==
        RuleFor(x => x.VehicleDto.ChassisNumber)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.VehicleDto.VehicleType)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.VehicleDto.VehicleAppointmentLocation)
            .MaximumLength(300)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        // == BuildingInfoDto ==
        RuleFor(x => x.BuildingInfoDto.BuildingType)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.BuildingInfoDto.UsableArea)
            .PrecisionScale(19, 2, true)
            .WithMessage("'{PropertyName}' must be a number with up to 17 total digits and up to 2 decimal places.");
        
        // == CondoInfoDto ==
        RuleFor(x => x.CondoInfoDto.BuildingNo)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.CondoInfoDto.CondoName)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        
        RuleFor(x => x.CondoInfoDto.RoomNo)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.CondoInfoDto.FloorNo)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.TitleAddress)
            .SetValidator(new TitleDocDtoValidator());

        RuleFor(x => x.DopaAddress)
            .SetValidator(new DopaAddressDtoValidator());

        RuleFor(x => x.TitleAddress)
            .SetValidator(new TitleDocDtoValidator());

        RuleFor(x => x.DopaAddress)
            .SetValidator(new DopaAddressDtoValidator());
    }
}
