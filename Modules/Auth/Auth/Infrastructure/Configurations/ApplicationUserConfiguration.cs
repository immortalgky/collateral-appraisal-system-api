using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Auth.Domain.Identity;

namespace Auth.Infrastructure.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(u => u.Position)
            .HasMaxLength(100);

        builder.Property(u => u.Department)
            .HasMaxLength(100);

        // AO code is the bank officer code (joins to auth.Officers.OfficerCode); cap at 10 to
        // match the AddAoCodeToApplicationUser migration (nvarchar(10)) and avoid model/snapshot drift.
        builder.Property(u => u.AoCode)
            .HasMaxLength(10);

        builder.Property(u => u.AuthSource)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(AuthSources.Local);

        builder.Property(u => u.CompanyId);

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.LastLoginAt);

        builder.Property(u => u.MustChangePassword)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.PasswordChangedAt);

        // Back RequireUniqueEmail with a real DB constraint. The validator's pre-insert FindByEmail
        // is a TOCTOU check — two concurrent creates with the same email can both pass it. A unique
        // index makes the second insert fail at the DB. Filtered on NOT NULL so multiple null/blank
        // emails are still allowed (and legacy rows with NULL email don't collide).
        // NOTE: this index cannot be applied while duplicate non-null emails exist — dedupe first.
        builder.HasIndex(u => u.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        // ASP.NET Identity only indexes NormalizedUserName. vw_TaskList joins users by raw
        // UserName twice per row (u.UserName = ISNULL(a.RequestedBy, r.Requestor) and
        // qrm.UserName = resolved.RmUsername), so those joins cannot seek the normalized index
        // and scan AspNetUsers while building the busiest list page. This index makes them seek;
        // INCLUDE covers the CONCAT(FirstName,' ',LastName) display projection.
        builder.HasIndex(u => u.UserName)
            .HasDatabaseName("IX_AspNetUsers_UserName")
            .IncludeProperties(u => new { u.FirstName, u.LastName });
    }
}
