using FluentValidation;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;

namespace Integration.Application.Validation;

// Reusable validators for the shared Request contract DTOs that arrive over the external
// Integration API. Lengths mirror the Request module's EF HasMaxLength configuration so an
// over-length value is rejected with a clean 400 at the boundary instead of surfacing as a
// DbUpdateException (500) at SaveChanges. A few free-text fields are nvarchar(max) in the DB
// (no column ceiling to mirror); those use a generous boundary cap of MaxFreeText, marked below.
internal static class ContractLengths
{
    // Boundary cap for fields that are nvarchar(max) in the DB (Comment, TitleDetail, title Notes).
    public const int MaxFreeText = 4000;
}

public class UserInfoDtoValidator : AbstractValidator<UserInfoDto>
{
    public UserInfoDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
    }
}

public class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator()
    {
        RuleFor(x => x.HouseNumber).MaximumLength(30);
        RuleFor(x => x.ProjectName).MaximumLength(100);
        RuleFor(x => x.Moo).MaximumLength(50);
        RuleFor(x => x.Soi).MaximumLength(50);
        RuleFor(x => x.Road).MaximumLength(50);
        RuleFor(x => x.SubDistrict).MaximumLength(10);
        RuleFor(x => x.District).MaximumLength(10);
        RuleFor(x => x.Province).MaximumLength(10);
        RuleFor(x => x.Postcode).MaximumLength(10);
    }
}

public class ContactDtoValidator : AbstractValidator<ContactDto>
{
    public ContactDtoValidator()
    {
        RuleFor(x => x.ContactPersonName).MaximumLength(100);
        RuleFor(x => x.ContactPersonPhone).MaximumLength(100);
        RuleFor(x => x.DealerCode).MaximumLength(20);
    }
}

public class AppointmentDtoValidator : AbstractValidator<AppointmentDto>
{
    public AppointmentDtoValidator()
    {
        RuleFor(x => x.AppointmentLocation).MaximumLength(4000);
    }
}

public class FeeDtoValidator : AbstractValidator<FeeDto>
{
    public FeeDtoValidator()
    {
        RuleFor(x => x.FeePaymentType).MaximumLength(10);
        RuleFor(x => x.FeeNotes).MaximumLength(4000);
    }
}

public class LoanDetailDtoValidator : AbstractValidator<LoanDetailDto>
{
    public LoanDetailDtoValidator()
    {
        RuleFor(x => x.BankingSegment).MaximumLength(10);
        RuleFor(x => x.LoanApplicationNumber).MaximumLength(20);
    }
}

public class RequestDetailDtoValidator : AbstractValidator<RequestDetailDto>
{
    public RequestDetailDtoValidator()
    {
        RuleFor(x => x.PrevAppraisalNumber).MaximumLength(20);

        // SetValidator no-ops when the nested property is null (FluentValidation built-in).
        RuleFor(x => x.LoanDetail!).SetValidator(new LoanDetailDtoValidator());
        RuleFor(x => x.Address!).SetValidator(new AddressDtoValidator());
        RuleFor(x => x.Contact!).SetValidator(new ContactDtoValidator());
        RuleFor(x => x.Appointment!).SetValidator(new AppointmentDtoValidator());
        RuleFor(x => x.Fee!).SetValidator(new FeeDtoValidator());
    }
}

public class RequestCustomerDtoValidator : AbstractValidator<RequestCustomerDto>
{
    public RequestCustomerDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(80);
        RuleFor(x => x.ContactNumber).MaximumLength(100);
    }
}

public class RequestPropertyDtoValidator : AbstractValidator<RequestPropertyDto>
{
    public RequestPropertyDtoValidator()
    {
        RuleFor(x => x.PropertyType).MaximumLength(10);
        RuleFor(x => x.BuildingType).MaximumLength(10);
    }
}

public class RequestTitleDocumentDtoValidator : AbstractValidator<RequestTitleDocumentDto>
{
    public RequestTitleDocumentDtoValidator()
    {
        RuleFor(x => x.DocumentType).MaximumLength(100);
        RuleFor(x => x.FileName).MaximumLength(255);
        RuleFor(x => x.Prefix).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.FilePath).MaximumLength(255);
        RuleFor(x => x.Source).MaximumLength(10);
        RuleFor(x => x.UploadedBy).MaximumLength(10);
        RuleFor(x => x.UploadedByName).MaximumLength(100);
    }
}

public class RequestTitleDtoValidator : AbstractValidator<RequestTitleDto>
{
    public RequestTitleDtoValidator()
    {
        RuleFor(x => x.CollateralType).NotEmpty().MaximumLength(10);

        // TitleDeedInfo
        RuleFor(x => x.TitleNumber).MaximumLength(200);
        RuleFor(x => x.TitleType).MaximumLength(50);
        RuleFor(x => x.TitleDetail).MaximumLength(ContractLengths.MaxFreeText); // nvarchar(max) in DB

        // LandLocationInfo
        RuleFor(x => x.BookNumber).MaximumLength(100);
        RuleFor(x => x.PageNumber).MaximumLength(100);
        RuleFor(x => x.LandParcelNumber).MaximumLength(100);
        RuleFor(x => x.SurveyNumber).MaximumLength(100);
        RuleFor(x => x.MapSheetNumber).MaximumLength(100);
        RuleFor(x => x.Rawang).MaximumLength(100);
        RuleFor(x => x.AerialMapName).MaximumLength(100);
        RuleFor(x => x.AerialMapNumber).MaximumLength(100);

        RuleFor(x => x.OwnerName).MaximumLength(500);

        // VehicleInfo
        RuleFor(x => x.VehicleType).MaximumLength(10);
        RuleFor(x => x.VehicleLocation).MaximumLength(300);
        RuleFor(x => x.VIN).MaximumLength(50);
        RuleFor(x => x.LicensePlateNumber).MaximumLength(20);

        // VesselInfo
        RuleFor(x => x.VesselType).MaximumLength(10);
        RuleFor(x => x.VesselLocation).MaximumLength(300);
        RuleFor(x => x.HIN).MaximumLength(50);
        RuleFor(x => x.VesselRegistrationNumber).MaximumLength(50);

        // MachineInfo
        RuleFor(x => x.RegistrationNumber).MaximumLength(50);
        RuleFor(x => x.MachineType).MaximumLength(10);
        RuleFor(x => x.InstallationStatus).MaximumLength(10);
        RuleFor(x => x.InvoiceNumber).MaximumLength(20);

        // BuildingInfo
        RuleFor(x => x.BuildingType).MaximumLength(10);

        // CondoInfo
        RuleFor(x => x.CondoName).MaximumLength(100);
        RuleFor(x => x.BuildingNumber).MaximumLength(100);
        RuleFor(x => x.RoomNumber).MaximumLength(30);
        RuleFor(x => x.FloorNumber).MaximumLength(10);

        RuleFor(x => x.Notes).MaximumLength(ContractLengths.MaxFreeText); // nvarchar(max) in DB

        RuleFor(x => x.TitleAddress!).SetValidator(new AddressDtoValidator());
        RuleFor(x => x.DopaAddress!).SetValidator(new AddressDtoValidator());

        RuleForEach(x => x.Documents).SetValidator(new RequestTitleDocumentDtoValidator());
    }
}

public class RequestDocumentDtoValidator : AbstractValidator<RequestDocumentDto>
{
    public RequestDocumentDtoValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(10);
        RuleFor(x => x.FileName).MaximumLength(255);
        RuleFor(x => x.Prefix).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.FilePath).MaximumLength(500);
        RuleFor(x => x.Source).MaximumLength(10);
        RuleFor(x => x.UploadedBy).MaximumLength(10);
        RuleFor(x => x.UploadedByName).MaximumLength(100);
    }
}

public class RequestCommentDtoValidator : AbstractValidator<RequestCommentDto>
{
    public RequestCommentDtoValidator()
    {
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(ContractLengths.MaxFreeText); // nvarchar(max) in DB
        RuleFor(x => x.CommentedBy).MaximumLength(10);
        RuleFor(x => x.CommentedByName).MaximumLength(100);
    }
}
