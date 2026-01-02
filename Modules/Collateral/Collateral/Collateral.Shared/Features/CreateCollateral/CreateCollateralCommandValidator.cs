using FluentValidation;

namespace Collateral.Collateral.Shared.Features.CreateCollateral;

public class CreateCollateralCommandValidator : AbstractValidator<CreateCollateralCommand>
{
    public CreateCollateralCommandValidator()
    {
        RuleFor(createCollateralCommand => createCollateralCommand.CollateralLand)
            .NotNull()
            .When(createCollateralCommand =>
                createCollateralCommand.CollatType.Equals(CollateralType.Land)
                || createCollateralCommand.CollatType.Equals(CollateralType.LandAndBuilding)
            )
            .ChildRules(y =>
            {
                y.RuleFor(y => y!.Coordinate)
                    .NotNull()
                    .ChildRules(z =>
                    {
                        z.RuleFor(z => z.Latitude).NotNull().WithMessage("Latitude is required.");
                        z.RuleFor(z => z.Longitude).NotNull().WithMessage("Longitude is required.");
                    })
                    .WithMessage("Coordinate is required.");
                y.RuleFor(y => y!.CollateralLocation)
                    .NotNull()
                    .ChildRules(z =>
                    {
                        z.RuleFor(z => z.SubDistrict)
                            .NotNull()
                            .WithMessage("SubDistrict is required.");
                        z.RuleFor(z => z.District).NotNull().WithMessage("District is required.");
                        z.RuleFor(z => z.Province).NotNull().WithMessage("Province is required.");
                        z.RuleFor(z => z.LandOffice)
                            .NotNull()
                            .WithMessage("LandOffice is required.");
                    })
                    .WithMessage("Coordinate is required.");
                y.RuleFor(y => y!.LandDesc).NotNull().WithMessage("LandDesc is required.");
            })
            .WithMessage(
                "Collateral land is required when the collateral type is land or land and building"
            );

        RuleFor(createCollateralCommand => createCollateralCommand.LandTitles)
            .NotNull()
            .When(createCollateralCommand =>
                createCollateralCommand.CollatType.Equals(CollateralType.Land)
                || createCollateralCommand.CollatType.Equals(CollateralType.LandAndBuilding)
            )
            .WithMessage(
                "Land titles is required when the collateral type is land or land and building"
            );

        RuleForEach(createCollateralCommand => createCollateralCommand.LandTitles)
            .ChildRules(landTitle =>
            {
                landTitle
                    .RuleFor(landTitle => landTitle.SeqNo)
                    .NotEmpty()
                    .WithMessage("SeqNo is required.");
                landTitle
                    .RuleFor(landTitle => landTitle.LandTitleDocumentDetail)
                    .NotEmpty()
                    .ChildRules(y =>
                    {
                        y.RuleFor(y => y.TitleNo).NotEmpty().WithMessage("TitleNo is required.");
                        y.RuleFor(y => y.BookNo).NotEmpty().WithMessage("BookNo is required.");
                        y.RuleFor(y => y.PageNo).NotEmpty().WithMessage("PageNo is required.");
                        y.RuleFor(y => y.LandNo).NotEmpty().WithMessage("LandNo is required.");
                        y.RuleFor(y => y.SurveyNo).NotEmpty().WithMessage("SurveyNo is required.");
                    })
                    .WithMessage("LandTitleDocumentDetail is required.");
                landTitle
                    .RuleFor(landTitle => landTitle.LandTitleArea)
                    .NotEmpty()
                    .WithMessage("LandTitleArea is required.");
                landTitle
                    .RuleFor(landTitle => landTitle.DocumentType)
                    .NotEmpty()
                    .WithMessage("DocumentType is required.");
                landTitle
                    .RuleFor(landTitle => landTitle.Rawang)
                    .NotEmpty()
                    .WithMessage("Rawang is required.");
                landTitle
                    .RuleFor(landTitle => landTitle.DocValidate)
                    .NotEmpty()
                    .WithMessage("DocValidate is required.");
            });

        RuleFor(createCollateralCommand => createCollateralCommand.CollateralBuilding)
            .NotNull()
            .When(createCollateralCommand =>
                createCollateralCommand.CollatType.Equals(CollateralType.Building)
                || createCollateralCommand.CollatType.Equals(CollateralType.LandAndBuilding)
            )
            .ChildRules(y =>
            {
                y.RuleFor(y => y!.BuildingNo).NotEmpty().WithMessage("BuildingNo is required.");
                y.RuleFor(y => y!.ModelName).NotEmpty().WithMessage("ModelName is required.");
                y.RuleFor(y => y!.HouseNo).NotEmpty().WithMessage("HouseNo is required.");
                y.RuleFor(y => y!.BuiltOnTitleNo)
                    .NotEmpty()
                    .WithMessage("BuiltOnTitleNo is required.");
                y.RuleFor(y => y!.Owner).NotEmpty().WithMessage("Owner is required.");
            })
            .WithMessage(
                "Collateral building is required when the collateral type is building or land and building."
            );

        RuleFor(createCollateralCommand => createCollateralCommand.CollateralCondo)
            .NotNull()
            .When(createCollateralCommand => createCollateralCommand.CollatType.Equals(CollateralType.Condo))
            .ChildRules(y =>
            {
                y.RuleFor(y => y!.CondoName).NotEmpty().WithMessage("CondoName is required.");
                y.RuleFor(y => y!.BuildingNo).NotEmpty().WithMessage("BuildingNo is required.");
                y.RuleFor(y => y!.ModelName).NotEmpty().WithMessage("ModelName is required.");
                y.RuleFor(y => y!.BuiltOnTitleNo)
                    .NotEmpty()
                    .WithMessage("BuiltOnTitleNo is required.");
                y.RuleFor(y => y!.CondoRegisNo).NotEmpty().WithMessage("CondoRegisNo is required.");
                y.RuleFor(y => y!.RoomNo).NotEmpty().WithMessage("RoomNo is required.");
                y.RuleFor(y => y!.FloorNo).NotEmpty().WithMessage("FloorNo is required.");
                y.RuleFor(y => y!.UsableArea).NotEmpty().WithMessage("UsableArea is required.");
                y.RuleFor(y => y!.CollateralLocation)
                    .NotEmpty()
                    .ChildRules(z =>
                    {
                        z.RuleFor(z => z.SubDistrict)
                            .NotEmpty()
                            .WithMessage("SubDistrict is required.");
                        z.RuleFor(z => z.District).NotEmpty().WithMessage("District is required.");
                        z.RuleFor(z => z.Province).NotEmpty().WithMessage("Province is required.");
                        z.RuleFor(z => z.LandOffice)
                            .NotEmpty()
                            .WithMessage("LandOffice is required.");
                    })
                    .WithMessage("CollateralLocation is required.");
                y.RuleFor(y => y!.Coordinate)
                    .NotEmpty()
                    .ChildRules(z =>
                    {
                        z.RuleFor(z => z.Latitude).NotNull().WithMessage("Latitude is required.");
                        z.RuleFor(z => z.Longitude).NotNull().WithMessage("Longitude is required.");
                    })
                    .WithMessage("Coordinate is required.");
                y.RuleFor(y => y!.Owner).NotEmpty().WithMessage("Owner is required.");
            })
            .WithMessage("Collateral condo is required when the collateral type is condo.");

        RuleFor(createCollateralCommand => createCollateralCommand.CollateralMachine)
            .NotNull()
            .When(createCollateralCommand => createCollateralCommand.CollatType.Equals(CollateralType.Machine))
            .ChildRules(y =>
            {
                y.RuleFor(y => y!.CollateralMachineProperty)
                    .NotEmpty()
                    .WithMessage("CollateralMachineProperty is required.");
                y.RuleFor(y => y!.CollateralMachineDetail)
                    .NotEmpty()
                    .WithMessage("CollateralMachineDetail is required.");
                y.RuleFor(y => y!.CollateralMachineSize)
                    .NotEmpty()
                    .WithMessage("CollateralMachineSize is required.");
                y.RuleFor(y => y!.ChassisNo).NotEmpty().WithMessage("ChassisNo is required.");
            })
            .WithMessage("Collateral machine is required when the collateral type is machine.");

        RuleFor(createCollateralCommand => createCollateralCommand.CollateralVehicle)
            .NotNull()
            .When(createCollateralCommand => createCollateralCommand.CollatType.Equals(CollateralType.Vehicle))
            .ChildRules(y =>
            {
                y.RuleFor(y => y!.CollateralVehicleProperty)
                    .NotEmpty()
                    .WithMessage("CollateralVehicleProperty is required.");
                y.RuleFor(y => y!.CollateralVehicleDetail)
                    .NotEmpty()
                    .WithMessage("CollateralVehicleDetail is required.");
                y.RuleFor(y => y!.CollateralVehicleSize)
                    .NotEmpty()
                    .WithMessage("CollateralVehicleSize is required.");
                y.RuleFor(y => y!.ChassisNo).NotEmpty().WithMessage("ChassisNo is required.");
            })
            .WithMessage("Collateral vehicle is required when the collateral type is vehicle.");

        RuleFor(createCollateralCommand => createCollateralCommand.CollateralVessel)
            .NotNull()
            .When(createCollateralCommand => createCollateralCommand.CollatType.Equals(CollateralType.Vessel))
            .ChildRules(y =>
            {
                y.RuleFor(y => y!.CollateralVesselProperty)
                    .NotEmpty()
                    .WithMessage("CollateralVesselProperty is required.");
                y.RuleFor(y => y!.CollateralVesselDetail)
                    .NotEmpty()
                    .WithMessage("CollateralVesselDetail is required.");
                y.RuleFor(y => y!.CollateralVesselSize)
                    .NotEmpty()
                    .WithMessage("CollateralVesselSize is required.");
            })
            .WithMessage("Collateral vessel is required when the collateral type is vessel.");

        RuleFor(createCollateralCommand => createCollateralCommand.ReqId).NotEmpty().WithMessage("Request ID is required.");
    }
}
