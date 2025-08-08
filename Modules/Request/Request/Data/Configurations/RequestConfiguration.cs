namespace Request.Data.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Requests.Models.Request>
{
    public void Configure(EntityTypeBuilder<Requests.Models.Request> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn();

        builder.OwnsOne(p => p.AppraisalNo, appraisalNo =>
        {
            appraisalNo.Property(p => p.Value).HasMaxLength(10).HasColumnName("AppraisalNo");
            appraisalNo.HasIndex(p => p.Value).IsUnique();
        });

        builder.OwnsOne(p => p.Status,
            status => { status.Property(p => p.Code).UseCodeConfig().HasColumnName("Status"); });

        // RequestDetails
        builder.OwnsOne(p => p.Detail, detail =>
        {
            detail.ToTable("RequestDetails");
            detail.WithOwner().HasForeignKey("RequestId");
            detail.HasKey("RequestId");

            detail.Property(p => p.Purpose).UseCodeConfig().HasColumnName("Purpose");
            detail.Property(p => p.HasAppraisalBook).HasColumnName("HasAppraisalBook");
            detail.Property(p => p.Priority).UseCodeConfig().HasColumnName("Priority");
            detail.Property(p => p.Channel).UseCodeConfig().HasColumnName("Channel");
            detail.Property(p => p.OccurConstInspec).HasColumnName("OccurConstInspec");

            detail.OwnsOne(p => p.Reference, prevAppraisal =>
            {
                prevAppraisal.Property(p => p.PrevAppraisalNo).HasMaxLength(10).HasColumnName("PrevAppraisalNo");
                prevAppraisal.Property(p => p.PrevAppraisalValue).UseMoneyConfig()
                    .HasColumnName("PrevAppraisalValue");
                prevAppraisal.Property(p => p.PrevAppraisalDate).HasColumnName("PrevAppraisalDate");
            });

            detail.OwnsOne(p => p.LoanDetail, loanDetail =>
            {
                loanDetail.Property(p => p.LoanApplicationNo).HasMaxLength(20).HasColumnName("LoanApplicationNo");
                loanDetail.Property(p => p.LimitAmt).UseMoneyConfig().HasColumnName("LimitAmt");
                loanDetail.Property(p => p.TotalSellingPrice).UseMoneyConfig().HasColumnName("TotalSellingPrice");
            });

            detail.OwnsOne(p => p.Address, address =>
            {
                address.Property(p => p.HouseNo).HasMaxLength(30).HasColumnName("HouseNo");
                address.Property(p => p.RoomNo).HasMaxLength(30).HasColumnName("RoomNo");
                address.Property(p => p.FloorNo).HasMaxLength(10).HasColumnName("FloorNo");
                address.Property(p => p.ProjectName).HasMaxLength(50).HasColumnName("LocationIdentifier");
                address.Property(p => p.Moo).HasMaxLength(50).HasColumnName("Moo");
                address.Property(p => p.Soi).HasMaxLength(50).HasColumnName("Soi");
                address.Property(p => p.Road).HasMaxLength(50).HasColumnName("Road");
                address.Property(p => p.SubDistrict).UseCodeConfig().HasColumnName("SubDistrict");
                address.Property(p => p.District).UseCodeConfig().HasColumnName("District");
                address.Property(p => p.Province).UseCodeConfig().HasColumnName("Province");
                address.Property(p => p.Postcode).UseCodeConfig().HasColumnName("Postcode");
            });

            detail.OwnsOne(p => p.Contact, contact =>
            {
                contact.Property(p => p.ContactPersonName).HasMaxLength(80).HasColumnName("ContactPersonName");
                contact.Property(p => p.ContactPersonContactNo).HasMaxLength(20)
                    .HasColumnName("ContactPersonContactNo");
                contact.Property(p => p.ProjectCode).HasMaxLength(10).HasColumnName("ProjectCode");
            });

            detail.OwnsOne(p => p.Requestor, requestor =>
            {
                requestor.Property(p => p.RequestorEmpId).UseCodeConfig().HasColumnName("RequestorEmpId");
                requestor.Property(p => p.RequestorName).HasMaxLength(40).HasColumnName("RequestorName");
                requestor.Property(p => p.RequestorEmail).HasMaxLength(40).HasColumnName("RequestorEmail");
                requestor.Property(p => p.RequestorContactNo).HasMaxLength(20).HasColumnName("RequestorContactNo");
                requestor.Property(p => p.RequestorAo).UseCodeConfig().HasColumnName("RequestorAo");
                requestor.Property(p => p.RequestorBranch).UseCodeConfig().HasColumnName("RequestorBranch");
                requestor.Property(p => p.RequestorBusinessUnit).UseCodeConfig().HasColumnName("RequestorBusinessUnit");
                requestor.Property(p => p.RequestorDepartment).UseCodeConfig().HasColumnName("RequestorDepartment");
                requestor.Property(p => p.RequestorSection).UseCodeConfig().HasColumnName("RequestorSection");
                requestor.Property(p => p.RequestorCostCenter).UseCodeConfig().HasColumnName("RequestorCostCenter");
            });

            detail.OwnsOne(p => p.Fee, feeInfo =>
            {
                feeInfo.Property(p => p.FeeType).UseCodeConfig().HasColumnName("FeeType");
                feeInfo.Property(p => p.FeeRemark).UseRemarkConfig().HasColumnName("FeeRemark");
            });
        });

        // RequestCustomers
        builder.OwnsMany(p => p.Customers, customer =>
        {
            customer.ToTable("RequestCustomers");
            customer.WithOwner().HasForeignKey("RequestId");

            customer.Property<long>("Id");
            customer.HasKey("Id");

            customer.Property(p => p.Name).HasMaxLength(80).HasColumnName("Name");
            customer.Property(p => p.ContactNumber).HasMaxLength(20).HasColumnName("ContactNumber");
        });

        // RequestProperties
        builder.OwnsMany(p => p.Properties, property =>
        {
            property.ToTable("RequestProperties");
            property.WithOwner().HasForeignKey("RequestId");

            property.Property<long>("Id");
            property.HasKey("Id");

            property.Property(p => p.PropertyType).UseCodeConfig().HasColumnName("PropertyType");
            property.Property(p => p.BuildingType).UseCodeConfig().HasColumnName("BuildingType");
            property.Property(p => p.SellingPrice).UseMoneyConfig().HasColumnName("SellingPrice");
        });
    }
}