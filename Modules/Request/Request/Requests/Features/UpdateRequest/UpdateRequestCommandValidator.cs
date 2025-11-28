namespace Request.Requests.Features.UpdateRequest;

public class UpdateRequestCommandValidator : AbstractValidator<UpdateRequestCommand>
{
    public UpdateRequestCommandValidator()
    {
        RuleFor(x => x.Purpose)
            .NotEmpty()
            .WithMessage("Purpose is required.");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .WithMessage("Priority is required.");

        RuleFor(x => x.SourceSystem.Channel)
            .NotEmpty()
            .WithMessage("Channel is required.");

        RuleFor(x => x.SourceSystem.RequestDate)
            .NotEmpty()
            .WithMessage("RequestDate is required.");

        RuleFor(x => x.SourceSystem.RequestBy)
            .NotEmpty()
            .WithMessage("RequestBy is required.");

        RuleFor(x => x.SourceSystem.RequestByName)
            .NotEmpty()
            .WithMessage("RequestByName is required.");

        RuleFor(x => x.SourceSystem.CreatedDate)
            .NotEmpty()
            .WithMessage("CreatedDate is required.");

        RuleFor(x => x.SourceSystem.Creator)
            .NotEmpty()
            .WithMessage("Creator is required.");

        RuleFor(x => x.SourceSystem.CreatorName)
            .NotEmpty()
            .WithMessage("CreatorName is required.");

        RuleFor(x => x.Detail.LoanDetail.BankingSegment)
            .NotEmpty()
            .WithMessage("BankingSegment is required.");

        RuleFor(x => x.Detail.Address.SubDistrict)
            .NotNull()
            .WithMessage("SubDistrict is required.");

        RuleFor(x => x.Detail.Contact.ContactPersonContactNo)
            .NotNull()
            .WithMessage("ContactPersonContactNo is required.");

        RuleFor(x => x.Detail.Contact.ContactPersonName)
            .NotNull()
            .WithMessage("ContactPersonName is required.");

        RuleFor(x => x.Detail.Fee.FeeType)
            .NotNull()
            .WithMessage("FeeType is required.");

        RuleFor(x => x.Detail.Appointment.AppointmentDateTime)
            .NotNull()
            .WithMessage("AppointmentDateTime is required.");

        RuleFor(x => x.Detail.Appointment.AppointmentLocation)
            .NotNull()
            .WithMessage("AppointmentLocation is required.");

        RuleFor(x => x.Detail.LoanDetail.FacilityLimit)
            .Must(FacilityLimit => FacilityLimit is null || FacilityLimit > 0)
            .WithMessage("FacilityLimit cannot be zero.");


        RuleFor(x => x.Customers)
            .NotNull()
            .WithMessage("Customers is required.")
            .Must(customers => customers.Count > 0)
            .WithMessage("At least one customer is required.");

        RuleFor(x => x.Properties)
            .NotNull()
            .WithMessage("Properties is required.")
            .Must(properties => properties.Count > 0)
            .WithMessage("At least one property is required.");
    }
}