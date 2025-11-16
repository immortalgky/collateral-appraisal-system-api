using Request.RequestComments.Models;

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

    public static LoanDetail ToDomain(this LoanDetailDto? dto)
    {
        return LoanDetail.Create(
            dto?.BankingSegment,
            dto?.LoanApplicationNo,
            dto?.FacilityLimit,
            dto?.TopUpLimit,
            dto?.OldFacilityLimit,
            dto?.TotalSellingPrice
        );
    }

    public static Address ToDomain(this AddressDto dto)
    {
        return Address.Create(
            dto.HouseNo,
            dto.RoomNo,
            dto.FloorNo,
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
            dto.FeePaymentType,
            dto.AbsorbedFee,
            dto.FeeNotes
        );
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
        return RequestComment.Create(dto.Id, dto.Comment, dto.CommentedBy, dto.CommentedByName);
    }

    public static TitleDeedInfo ToDomain(this TitleDeedInfoDto dto)
    {
        return TitleDeedInfo.Create(dto.TitleNo, dto.DeedType, dto.TitleDetail);
    }

    public static SurveyInfo ToDomain(this SurveyInfoDto dto)
    {
        return SurveyInfo.Create(dto.Rawang, dto.LandNo, dto.SurveyNo);
    }

    public static LandArea ToDomain(this LandAreaDto dto)
    {
        return LandArea.Of(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa);
    }

    public static VehicleInfo ToDomain(this VehicleDto dto)
    {
        return VehicleInfo.Create(dto.VehicleType, dto.VehicleAppointmentLocation, dto.ChassisNumber);
    }

    public static MachineInfo ToDomain(this MachineDto dto)
    {
        return MachineInfo.Create(dto.MachineryStatus, dto.MachineryType, dto.InstallationStatus, dto.InvoiceNumber, dto.NumberOfMachinery);
    }

    public static BuildingInfo ToDomain(this BuildingInfoDto dto)
    {
        return BuildingInfo.Create(dto.BuildingType, dto.UsableArea, dto.NumberOfBuilding);
    }

    public static CondoInfo ToDomain(this CondoInfoDto dto)
    {
        return CondoInfo.Create(dto.CondoName, dto.BuildingNo, dto.RoomNo, dto.FloorNo);
    }

}