using Request.RequestDocuments.Features.AddRequestDocument;
using Request.Contracts.RequestDocuments.Dto;


namespace Request.Extensions;

public static class DtoExtensions
{
    public static Reference ToDomain(this ReferenceDto? dto)
    {
        return Reference.Create(
            dto?.PrevAppraisalNo,
            dto?.PrevAppraisalValue,
            dto?.PrevAppraisalDate
        );
    }

    public static RequestDetail ToDomain(this RequestDetailDto? dto)
    {
        return RequestDetail.Create(
            dto.HasAppraisalBook,
            dto.PrevAppraisalNo,
            dto.LoanDetail.ToDomain(),
            dto.Address.ToDomain(),
            dto.Contact.ToDomain(),
            dto.Appointment.ToDomain(),
            dto.Fee.ToDomain()
        );
    }

    public static LoanDetail ToDomain(this LoanDetailDto? dto)
    {
        return LoanDetail.Create(
            dto?.LoanApplicationNo,
            dto?.BankingSegment,
            dto?.FacilityLimit,
            dto?.AdditionalFacilityLimit,
            dto?.PreviousFacilityLimit,
            dto?.TotalSellingPrice
        );
    }

    public static Address ToDomain(this AddressDto dto)
    {
        return Address.Create(
            dto.HouseNo,
            dto.RoomNo,
            dto.FloorNo,
            dto.BuildingNo,
            dto.ProjectName,
            dto.Moo,
            dto.Soi,
            dto.Road,
            dto.SubDistrict,
            dto.District,
            dto.Province,
            dto.Postcode
        );
    }

    public static Contact ToDomain(this ContactDto dto)
    {
        return Contact.Create(
            dto.ContactPersonName,
            dto.ContactPersonContactNo,
            dto.ProjectCode
        );
    }

    public static Fee ToDomain(this FeeDto dto)
    {
        return Fee.Create(
            dto.FeeType,
            dto.FeeNote,
            dto.BankAbsorbAmt
        );
    }

    public static SoftDelete ToDomain(this SoftDeleteDto dto)
    {
        return SoftDelete.Create(
            dto.IsDeleted,
            dto.DeletedOn,
            dto.DeletedBy
        );
    }

    public static SourceSystem ToDomain(this SourceSystemDto dto)
    {
        return SourceSystem.Create(
            dto.Channel,
            dto.RequestDate,
            dto.RequestBy,
            dto.RequestByName,
            dto.CreatedDate,
            dto.Creator,
            dto.CreatorName);
    }

    public static Requestor ToDomain(this RequestorDto dto)
    {
        return Requestor.Create(
            dto.RequestorEmpId,
            dto.RequestorName,
            dto.RequestorEmail,
            dto.RequestorContactNo,
            dto.RequestorAo,
            dto.RequestorBranch,
            dto.RequestorBusinessUnit,
            dto.RequestorDepartment,
            dto.RequestorSection,
            dto.RequestorCostCenter
        );
    }


    public static Appointment ToDomain(this AppointmentDto dto)
    {
        return Appointment.Create(
            dto.AppointmentDateTime,
            dto.AppointmentLocation
        );
    }

    public static RequestCustomer ToDomain(this RequestCustomerDto dto)
    {
        return RequestCustomer.Create(
            dto.Name,
            dto.ContactNumber
        );
    }

    public static RequestProperty ToDomain(this RequestPropertyDto dto)
    {
        return RequestProperty.Of(
            dto.PropertyType,
            dto.BuildingType,
            dto.SellingPrice
        );
    }

    public static RequestComment ToDomain(this RequestCommentDto dto)
    {
        return RequestComment.Create(dto.Id, dto.Comment);
    }

    public static DocumentClassification ToDomain(this DocumentClassificationDto dto)
    {
        return DocumentClassification.Create(
            dto.DocumentType,
            dto.IsRequired
        );
    }

    public static UploadInfo ToDomain(this UploadInfoDto dto)
    {
        return UploadInfo.Create(
            dto.UploadedBy,
            dto.UploadedByName,
            dto.UploadedAt
        );
    }
}