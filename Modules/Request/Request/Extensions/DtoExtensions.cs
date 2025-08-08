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
            dto?.LoanApplicationNo,
            dto?.LimitAmt,
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
            dto.FeeRemark
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
        return RequestComment.Create(0, dto.Comment);
    }
}