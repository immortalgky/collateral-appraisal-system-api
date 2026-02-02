namespace Request.Infrastructure.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Domain.Requests.Request>
{
    public void Configure(EntityTypeBuilder<Domain.Requests.Request> builder)
    {
        builder.HasKey(p => p.Id);
        builder.OwnsOne(p => p.RequestNumber,
            requestNumber =>
            {
                requestNumber.Property(p => p.Value).HasMaxLength(255).HasColumnName("RequestNumber");
                requestNumber.HasIndex(p => p.Value).HasDatabaseName("IX_Request_RequestNumber");
            });

        builder.Property(p => p.Purpose).HasMaxLength(10);
        builder.Property(p => p.Channel).HasMaxLength(10);
        builder.OwnsOne(p => p.Requestor, requestor =>
        {
            requestor.Property(p => p.UserId).HasMaxLength(10).HasColumnName("Requestor");
            requestor.Property(p => p.Username).HasMaxLength(100).HasColumnName("RequestorName");
            requestor.HasIndex(p => p.UserId).HasFilter("[IsDeleted] = 0").HasDatabaseName("IX_Request_Requestor");
        });
        builder.OwnsOne(p => p.Creator, requestor =>
        {
            requestor.Property(p => p.UserId).HasMaxLength(10).HasColumnName("Creator");
            requestor.Property(p => p.Username).HasMaxLength(100).HasColumnName("CreatorName");
        });
        builder.Property(p => p.Priority).HasMaxLength(255);

        builder.OwnsOne(p => p.SoftDelete, sd =>
        {
            sd.Property(p => p.IsDeleted).HasColumnName("IsDeleted");
            sd.Property(p => p.DeletedAt).HasColumnName("DeletedAt");
            sd.Property(p => p.DeletedBy).HasMaxLength(10).HasColumnName("DeletedBy");
        });

        builder.OwnsOne(p => p.Status, status =>
        {
            status.Property(p => p.Code).HasMaxLength(10).HasColumnName("Status");
            status.HasIndex(p => p.Code).HasFilter("[IsDeleted] = 0").HasDatabaseName("IX_Request_Status");
        });

        // RequestDetails
        builder.OwnsOne(r => r.Detail, detail =>
        {
            detail.ToTable("RequestDetails");
            detail.WithOwner().HasForeignKey("RequestId");
            detail.HasKey("RequestId");

            detail.Property(p => p.HasAppraisalBook).HasColumnName("HasAppraisalBook");
            detail.Property(p => p.PrevAppraisalId).HasColumnName("PrevAppraisalId");
            detail.OwnsOne(p => p.LoanDetail,
                loanDetail =>
                {
                    loanDetail.Property(p => p.LoanApplicationNumber).HasMaxLength(20)
                        .HasColumnName("LoanApplicationNumber");
                    loanDetail.Property(p => p.BankingSegment).HasMaxLength(10).HasColumnName("BankingSegment");
                    loanDetail.Property(p => p.FacilityLimit).HasPrecision(19, 4).HasColumnName("FacilityLimit");
                    loanDetail.Property(p => p.AdditionalFacilityLimit).HasPrecision(19, 4)
                        .HasColumnName("AdditionalFacilityLimit");
                    loanDetail.Property(p => p.PreviousFacilityLimit).HasPrecision(19, 4)
                        .HasColumnName("PreviousFacilityLimit");
                    loanDetail.Property(p => p.TotalSellingPrice).HasPrecision(19, 4)
                        .HasColumnName("TotalSellingPrice");

                    //Index
                    loanDetail
                        .HasIndex(p => p.LoanApplicationNumber)
                        .HasFilter("[LoanApplicationNumber] IS NOT NULL")
                        .HasDatabaseName("IX_Request_LoanApplicationNumber");
                });

            detail.OwnsOne(p => p.Address, address =>
            {
                address.Property(p => p.HouseNumber).HasMaxLength(30).HasColumnName("HouseNumber");
                address.Property(p => p.ProjectName).HasMaxLength(100).HasColumnName("ProjectName");
                address.Property(p => p.Moo).HasMaxLength(50).HasColumnName("Moo");
                address.Property(p => p.Soi).HasMaxLength(50).HasColumnName("Soi");
                address.Property(p => p.Road).HasMaxLength(50).HasColumnName("Road");
                address.Property(p => p.SubDistrict).HasMaxLength(10).HasColumnName("SubDistrict");
                address.Property(p => p.District).HasMaxLength(10).HasColumnName("District");
                address.Property(p => p.Province).HasMaxLength(10).HasColumnName("Province");
                address.Property(p => p.Postcode).HasMaxLength(10).HasColumnName("Postcode");
            });

            detail.OwnsOne(p => p.Appointment, appointment =>
            {
                appointment.Property(p => p.AppointmentDateTime).HasColumnName("AppointmentDateTime");
                appointment.Property(p => p.AppointmentLocation).HasMaxLength(4000)
                    .HasColumnName("AppointmentLocation");
            });

            detail.OwnsOne(p => p.Contact, contact =>
            {
                contact.Property(p => p.ContactPersonName).HasMaxLength(100).HasColumnName("ContactPersonName");
                contact.Property(p => p.ContactPersonPhone).HasMaxLength(20).HasColumnName("ContactPersonPhone");
                contact.Property(p => p.DealerCode).HasMaxLength(20).HasColumnName("DealerCode");
            });

            detail.OwnsOne(p => p.Fee, feeInfo =>
            {
                feeInfo.Property(p => p.FeePaymentType).HasMaxLength(10).HasColumnName("FeePaymentType");
                feeInfo.Property(p => p.AbsorbedAmount).HasPrecision(19, 4).HasColumnName("AbsorbedAmount");
                feeInfo.Property(p => p.FeeNotes).HasMaxLength(4000).HasColumnName("FeeNotes");
            });
        });

        // RequestCustomers
        builder.OwnsMany(r => r.Customers, customer =>
        {
            customer.ToTable("RequestCustomers");
            customer.WithOwner().HasForeignKey("RequestId");
            customer.Property<long>("Id");
            customer.HasKey("Id");

            customer.Property(p => p.Name).HasMaxLength(80).HasColumnName("Name");
            customer.Property(p => p.ContactNumber).HasMaxLength(20).HasColumnName("ContactNumber");

            //Index
            customer.HasIndex(p => p.Name).HasDatabaseName("IX_RequestCustomer_Name");
        });

        // RequestProperties
        builder.OwnsMany(r => r.Properties, property =>
        {
            property.ToTable("RequestProperties");
            property.WithOwner().HasForeignKey("RequestId");
            property.Property<long>("Id");
            property.HasKey("Id");

            property.Property(p => p.PropertyType).HasMaxLength(10).HasColumnName("PropertyType");
            property.Property(p => p.BuildingType).HasMaxLength(10).HasColumnName("BuildingType");
            property.Property(p => p.SellingPrice).HasPrecision(19, 4).HasColumnName("SellingPrice");

            //Index
            property.HasIndex(p => p.PropertyType).HasDatabaseName("IX_RequestProperty_PropertyType");
        });

        // RequestDocuments
        builder.OwnsMany(r => r.Documents, doc =>
            new RequestDocumentConfiguration().Configure(doc));

        // Index
        builder
            .HasIndex(p => p.RequestedAt)
            .IsDescending(true)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Request_RequestedAt");
    }
}