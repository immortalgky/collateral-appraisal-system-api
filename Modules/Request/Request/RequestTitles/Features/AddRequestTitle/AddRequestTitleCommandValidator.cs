namespace Request.RequestTitles.Features.AddRequestTitle;

public class AddRequestTitleCommandValidator : AbstractValidator<AddRequestTitleCommand>
{
    public AddRequestTitleCommandValidator()
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

public class TitleDocDtoValidator : AbstractValidator<AddressDto>
{
    public TitleDocDtoValidator()
    {
        RuleFor(a => a.HouseNo)
            .MaximumLength(30)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.RoomNo)
            .Null()
            .WithMessage("'{PropertyName}' must be null");
        
        RuleFor(a => a.FloorNo)
            .Null()
            .WithMessage("'{PropertyName}' must be null");
        
        RuleFor(a => a.ProjectName)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(a => a.Moo)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(a => a.Soi)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(a => a.Road)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.SubDistrict)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.District)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.Province)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.Postcode)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

    }
}

public class DopaAddressDtoValidator : AbstractValidator<AddressDto>
{
    public DopaAddressDtoValidator()
    {
        RuleFor(a => a.HouseNo)
            .MaximumLength(30)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.RoomNo)
            .Null()
            .WithMessage("'{PropertyName}' must be null");
        
        RuleFor(a => a.FloorNo)
            .Null()
            .WithMessage("'{PropertyName}' must be null");
        
        RuleFor(a => a.ProjectName)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(a => a.Moo)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(a => a.Soi)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(a => a.Road)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.SubDistrict)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.District)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.Province)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(a => a.Postcode)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

    }
}