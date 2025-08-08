namespace Request.Configurations;

public static class MappingConfiguration
{
    public static void ConfigureMappings()
    {
        TypeAdapterConfig<LoanDetailDto, LoanDetail>
            .NewConfig()
            .ConstructUsing(src => LoanDetail.Create(
                src.LoanApplicationNo,
                src.LimitAmt,
                src.TotalSellingPrice
            ));

        TypeAdapterConfig<ReferenceDto, Reference>
            .NewConfig()
            .ConstructUsing(src => Reference.Create(
                src.PrevAppraisalNo,
                src.PrevAppraisalValue,
                src.PrevAppraisalDate
            ));

        TypeAdapterConfig<AddressDto, Address>
            .NewConfig()
            .ConstructUsing(src => Address.Create(
                src.HouseNo,
                src.RoomNo,
                src.FloorNo,
                src.BuildingNo,
                src.ProjectName,
                src.Moo,
                src.Soi,
                src.Road,
                src.SubDistrict,
                src.District,
                src.Province,
                src.Postcode
            ));

        TypeAdapterConfig<ContactDto, Contact>
            .NewConfig()
            .ConstructUsing(src => Contact.Create(
                src.ContactPersonName,
                src.ContactPersonContactNo,
                src.ProjectCode
            ));

        TypeAdapterConfig<FeeDto, Fee>
            .NewConfig()
            .ConstructUsing(src => Fee.Create(
                src.FeeType,
                src.FeeRemark
            ));

        TypeAdapterConfig<RequestorDto, Requestor>
            .NewConfig()
            .ConstructUsing(src => Requestor.Create(
                src.RequestorEmpId,
                src.RequestorName,
                src.RequestorEmail,
                src.RequestorContactNo,
                src.RequestorAo,
                src.RequestorBranch,
                src.RequestorBusinessUnit,
                src.RequestorDepartment,
                src.RequestorSection,
                src.RequestorCostCenter
            ));

        TypeAdapterConfig<RequestCustomerDto, RequestCustomer>
            .NewConfig()
            .ConstructUsing(src => RequestCustomer.Create(
                src.Name,
                src.ContactNumber
            ));

        TypeAdapterConfig<RequestPropertyDto, RequestProperty>
            .NewConfig()
            .ConstructUsing(src => RequestProperty.Of(
                src.PropertyType,
                src.BuildingType,
                src.SellingPrice
            ));
    }
}