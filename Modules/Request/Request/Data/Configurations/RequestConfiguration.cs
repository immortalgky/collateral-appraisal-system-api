using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Request.Data.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Requests.Models.Request>
{
    public void Configure(EntityTypeBuilder<Requests.Models.Request> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasValueGenerator(typeof(SequentialGuidValueGenerator));

        builder.OwnsOne(r => r.RequestNumber, requestNumber =>
        {
            requestNumber.Property(rn => rn.Value).HasMaxLength(15).HasColumnName("RequestNumber"); 
            requestNumber.HasIndex(rn => rn.Value).IsUnique();
        });

        builder.Property(r => r.Purpose).HasMaxLength(10);

        builder.OwnsOne(r => r.Source, source =>
        {
            source.Property(s => s.Channel).HasMaxLength(10).HasColumnName("Channel");
            source.Property(s => s.RequestDate).HasColumnName("RequestDate");
            source.Property(s => s.RequestedBy).HasMaxLength(10).HasColumnName("RequestedBy"); // key link to User
            source.Property(s => s.RequestedByName).HasMaxLength(100).HasColumnName("RequestedByName");
        });

        builder.Property(r => r.Priority).UseCodeConfig();

        builder.OwnsOne(r => r.Status,
            status => { status.Property(p => p.Code).HasMaxLength(20).HasColumnName("Status"); });

        builder.Property(r => r.SubmittedAt);
        builder.Property(r => r.CompletedAt);

        builder.OwnsOne(r => r.Deletion, deletion =>
        {
            deletion.Property(d => d.IsDeleted).HasColumnName("IsDeleted");
            deletion.Property(d => d.DeletedOn).HasColumnName("DeletedOn");
            deletion.Property(d => d.DeletedBy).HasMaxLength(100).HasColumnName("DeletedBy");
        });

        builder.Property(r => r.IsPMA);

        // RequestDetails
        builder.OwnsOne(r => r.Detail, detail =>
        {
            detail.ToTable("RequestDetails");
            detail.WithOwner().HasForeignKey("RequestId");
            detail.HasKey("RequestId");

            detail.Property(d => d.HasOwnAppraisalBook).HasColumnName("HasAppraisalBook");

            detail.Property(d => d.PreviousAppraisalId).HasColumnName("PreviousAppraisalId");

            detail.OwnsOne(d => d.LoanDetail, loanDetail =>
            {
                loanDetail.Property(l => l.LoanApplicationNo).HasMaxLength(20).HasColumnName("LoanApplicationNo");
                loanDetail.Property(l => l.BankingSegment).HasMaxLength(10).HasColumnName("BankingSegment");
                loanDetail.Property(l => l.FacilityLimit).UseMoneyConfig().HasColumnName("LimitAmt");
                loanDetail.Property(l => l.TopUpLimit).UseMoneyConfig().HasColumnName("TopUpLimit");
                loanDetail.Property(l => l.OldFacilityLimit).UseMoneyConfig().HasColumnName("OldFacilityLimit");
                loanDetail.Property(l => l.TotalSellingPrice).UseMoneyConfig().HasColumnName("TotalSellingPrice");
            });

            detail.OwnsOne(r => r.Address, address =>
            {
                address.Property(a => a.HouseNo).HasMaxLength(30).HasColumnName("HouseNo");
                address.Property(a => a.RoomNo).HasMaxLength(30).HasColumnName("RoomNo");
                address.Property(a => a.FloorNo).HasMaxLength(10).HasColumnName("FloorNo");
                address.Property(a => a.ProjectName).HasMaxLength(100).HasColumnName("ProjectName");
                address.Property(a => a.Moo).HasMaxLength(50).HasColumnName("Moo");
                address.Property(a => a.Soi).HasMaxLength(100).HasColumnName("Soi");
                address.Property(a => a.Road).HasMaxLength(100).HasColumnName("Road");
                address.Property(a => a.SubDistrict).HasMaxLength(50).HasColumnName("SubDistrict");
                address.Property(a => a.District).HasMaxLength(50).HasColumnName("District");
                address.Property(a => a.Province).HasMaxLength(50).HasColumnName("Province");
                address.Property(a => a.Postcode).HasMaxLength(10).HasColumnName("Postcode");
            });

            detail.OwnsOne(r => r.Contact, contact =>
            {
                contact.Property(c => c.ContactPersonName).HasMaxLength(100).HasColumnName("ContactPersonName");
                contact.Property(c => c.ContactPersonPhone).HasMaxLength(40)
                    .HasColumnName("ContactPersonPhone");
                contact.Property(c => c.ProjectCode).HasMaxLength(10).HasColumnName("ProjectCode");
            });

            detail.OwnsOne(r => r.Appointment, appointment =>
            {
                appointment.Property(a => a.AppointmentDateTime).HasColumnName("AppointmentDateTime");
                appointment.Property(a => a.AppointmentLocation).HasMaxLength(200).HasColumnName("AppointmentLocation");
            });

            detail.OwnsOne(r => r.Fee, feeInfo =>
            {
                feeInfo.Property(f => f.FeePaymentType).HasMaxLength(10).HasColumnName("FeePaymentType");
                feeInfo.Property(f => f.AbsorbedFee).UseMoneyConfig().HasColumnName("AbsorbedFee");
                feeInfo.Property(f => f.FeeNotes).UseRemarkConfig().HasColumnName("FeeNotes");
            });
        });

        // RequestCustomers
        builder.OwnsMany(r => r.Customers, customer =>
        {
            customer.ToTable("RequestCustomers");
            customer.WithOwner().HasForeignKey("RequestId");

            customer.Property<long>("Id");
            customer.HasKey("Id");

            customer.Property(c => c.Name).HasMaxLength(100).HasColumnName("Name");
            customer.Property(c => c.ContactNumber).HasMaxLength(20).HasColumnName("ContactNumber");
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
        });
    }
}