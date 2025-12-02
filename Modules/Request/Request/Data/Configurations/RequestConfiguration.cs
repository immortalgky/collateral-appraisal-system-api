using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Request.Data.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Requests.Models.Request>
{
    public void Configure(EntityTypeBuilder<Requests.Models.Request> builder)
    {
        builder.HasKey(p => p.Id);
        builder.OwnsOne(p => p.AppraisalNo, appraisalNo =>
        {
            appraisalNo.Property(p => p.Value).HasMaxLength(10).HasColumnName("RequestNumber");

            // Index
            appraisalNo.HasIndex(p => p.Value).HasDatabaseName("IX_Request_RequestNumber").IsUnique();
        });

        builder.Property(p => p.Purpose).UseCodeConfig().HasColumnName("Purpose");
        builder.OwnsOne(p => p.SourceSystem, sourceSystem =>
        {
            sourceSystem.Property(p => p.Channel).HasColumnName("Channel");
            sourceSystem.Property(p => p.RequestDate).HasColumnName("RequestDate");
            sourceSystem.Property(p => p.RequestBy).HasColumnName("RequestBy");
            sourceSystem.Property(p => p.RequestByName).HasColumnName("RequestByName");
            sourceSystem.Property(p => p.CreatedDate).HasColumnName("CreatedDate");
            sourceSystem.Property(p => p.Creator).HasColumnName("Creator");
            sourceSystem.Property(p => p.CreatorName).HasColumnName("CreatorName");

            //Index
            sourceSystem.HasIndex(p => p.RequestBy).HasDatabaseName("IX_Request_RequestedBy")
                .HasFilter("[IsDeleted] = 0");
            sourceSystem.HasIndex(p => p.RequestDate).HasDatabaseName("IX_Request_RequestDate")
                .IsDescending(true)
                .HasFilter("[IsDeleted] = 0");
        });
        builder.Property(p => p.Priority).HasColumnName("Priority");
        builder.Property(p => p.IsPMA).HasColumnName("IsPMA");
        builder.OwnsOne(p => p.SoftDelete, softDeleted =>
        {
            softDeleted.Property(p => p.IsDeleted).HasColumnName("IsDeleted");
            softDeleted.Property(p => p.DeletedOn).HasColumnName("DeletedOn");
            softDeleted.Property(p => p.DeletedBy).HasColumnName("DeletedBy");
        });
        builder.OwnsOne(p => p.Status,
            status =>
            {
                status.Property(p => p.Code).UseCodeConfig().HasColumnName("Status");
                status.Property(p => p.SubmittedAt).HasColumnName("SubmittedAt");
                status.Property(p => p.CompletedAt).HasColumnName("CompletedAt");

                //Index
                status.HasIndex(p => p.Code).HasDatabaseName("IX_Request_Status").HasFilter("[IsDeleted] = 0");
            });

        // RequestDetails
        builder.OwnsOne(r => r.Detail, detail =>
        {
            detail.ToTable("RequestDetails");
            detail.WithOwner().HasForeignKey("RequestId");
            detail.HasKey("RequestId");
            detail.Property(p => p.HasAppraisalBook).HasColumnName("HasAppraisalBook");
            detail.Property(p => p.PrevAppraisalNo).HasMaxLength(10).HasColumnName("PrevAppraisalNo");
            detail.OwnsOne(p => p.LoanDetail,
                loanDetail =>
                {
                    loanDetail.Property(p => p.LoanApplicationNo).HasMaxLength(20).HasColumnName("LoanApplicationNo");
                    loanDetail.Property(p => p.BankingSegment).UseCodeConfig().HasColumnName("BankingSegment");
                    loanDetail.Property(p => p.FacilityLimit).UseMoneyConfig().HasColumnName("FacilityLimit");
                    loanDetail.Property(p => p.AdditionalFacilityLimit).UseMoneyConfig()
                        .HasColumnName("AdditionalFacilityLimit");
                    loanDetail.Property(p => p.PreviousFacilityLimit).UseMoneyConfig()
                        .HasColumnName("PreviousFacilityLimit");
                    loanDetail.Property(p => p.TotalSellingPrice).UseMoneyConfig().HasColumnName("TotalSellingPrice");

                    //Index
                    loanDetail.HasIndex(p => p.LoanApplicationNo)
                        .HasDatabaseName("IX_Request_LoanApplicationNumber")
                        .HasFilter("[LoanApplicationNo] IS NOT NULL");
                });
            detail.OwnsOne(p => p.Address, address =>
            {
                address.Property(p => p.HouseNo).HasMaxLength(30).HasColumnName("HouseNo");
                address.Property(p => p.RoomNo).HasMaxLength(30).HasColumnName("RoomNo");
                address.Property(p => p.FloorNo).HasMaxLength(10).HasColumnName("FloorNo");
                address.Property(p => p.ProjectName).HasMaxLength(100).HasColumnName("LocationIdentifier");
                address.Property(p => p.Moo).HasMaxLength(50).HasColumnName("Moo");
                address.Property(p => p.Soi).HasMaxLength(50).HasColumnName("Soi");
                address.Property(p => p.Road).HasMaxLength(50).HasColumnName("Road");
                address.Property(p => p.SubDistrict).UseCodeConfig().HasColumnName("SubDistrict");
                address.Property(p => p.District).UseCodeConfig().HasColumnName("District");
                address.Property(p => p.Province).UseCodeConfig().HasColumnName("Province");
                address.Property(p => p.Postcode).UseCodeConfig().HasColumnName("Postcode");
            });

            detail.OwnsOne(p => p.Appointment, appointment =>
            {
                appointment.Property(p => p.AppointmentDateTime).HasColumnName("AppointmentDateTime");
                appointment.Property(p => p.AppointmentLocation).HasMaxLength(4000)
                    .HasColumnName("AppointmentLocation");
            });

            detail.OwnsOne(p => p.Fee, feeInfo =>
            {
                feeInfo.Property(p => p.FeeType).UseCodeConfig().HasColumnName("FeeType");
                feeInfo.Property(p => p.FeeNotes).UseRemarkConfig().HasColumnName("FeeRemark");
                feeInfo.Property(p => p.BankAbsorbAmt).UseMoneyConfig().HasColumnName("BankAbsorbAmount");
            });
        });

        // RequestCustomers
        builder.OwnsMany(r => r.Customers, customer =>
        {
            customer.ToTable("RequestCustomers");
            customer.WithOwner().HasForeignKey("RequestId");
            customer.Property<long>("Id");
            customer.HasKey("Id");
            customer.Property(p => p.Name).HasMaxLength(80).HasColumnName("CustomerName");
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
            property.Property(p => p.PropertyType).UseCodeConfig().HasColumnName("PropertyType");
            property.Property(p => p.BuildingType).UseCodeConfig().HasColumnName("BuildingType");
            property.Property(p => p.SellingPrice).UseMoneyConfig().HasColumnName("SellingPrice");

            //Index
            property.HasIndex(p => p.PropertyType).HasDatabaseName("IX_RequestProperty_PropertyType");
        });

        builder.HasMany(p => p.Titles)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(p => p.Comments)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Documents).WithOne().OnDelete(DeleteBehavior.Cascade);
    }
}