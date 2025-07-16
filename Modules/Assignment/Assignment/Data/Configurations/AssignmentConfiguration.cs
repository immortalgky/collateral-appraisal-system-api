namespace Assignment.Data.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignments.Models.Assignment>
{
    public void Configure(EntityTypeBuilder<Assignments.Models.Assignment> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("AssignmentId").UseIdentityColumn();

        builder.OwnsOne(p => p.Detail, detail =>
        {
            detail.Property(p => p.ReqID).HasColumnName("RequestId");
            detail.Property(p => p.AssignmentMethod).HasColumnName("AssignmentMethod").HasMaxLength(10);
            detail.Property(p => p.ExternalCompanyID).HasColumnName("ExternalCompanyID").HasMaxLength(10);
            detail.Property(p => p.ExternalCompanyAssignType).HasColumnName("ExternalCompanyAssignType");
            detail.Property(p => p.ExtApprStaff).HasColumnName("ExtApprStaff").HasMaxLength(10);
            detail.Property(p => p.ExtApprStaffAssignmentType).HasColumnName("ExtApprStaffAssignmentType").HasMaxLength(10);
            detail.Property(p => p.IntApprStaff).HasColumnName("IntApprStaff").HasMaxLength(10);
            detail.Property(p => p.IntApprStaffAssignmentType).HasColumnName("IntApprStaffAssignmentType").HasMaxLength(10);
            detail.Property(p => p.Remark).HasColumnName("Remark");
        });
    }
}