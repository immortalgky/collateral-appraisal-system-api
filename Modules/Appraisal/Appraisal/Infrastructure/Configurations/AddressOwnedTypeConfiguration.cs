using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// The two addresses every land and condo detail row carries, mapped identically on both.
///
/// Shared rather than repeated per entity because the pair has to stay in step: the deed address
/// and the DOPA address are resolved against different masters (see AppraisalSearchPredicate), and
/// the two configurations drifting apart is a silent bug — the column still maps, it just carries
/// or omits an index nobody notices until a search gets slow.
/// </summary>
internal static class AddressOwnedTypeConfiguration
{
    /// <summary>
    /// The deed address (ที่อยู่ตามโฉนด), Title-mastered, stored in the unprefixed columns.
    /// </summary>
    public static void ConfigureDeedAddress<TOwner>(OwnedNavigationBuilder<TOwner, Address> addr)
        where TOwner : class
    {
        addr.Property(a => a.SubDistrict).HasColumnName("SubDistrict").HasMaxLength(100);
        addr.Property(a => a.District).HasColumnName("District").HasMaxLength(100);
        addr.Property(a => a.Province).HasColumnName("Province").HasMaxLength(100);

        // Global search resolves a typed Thai place name to geocodes and then looks the codes up
        // here. Without these the search arm scans the whole detail table on every address search:
        // measured 562 ms for a term matching 11 sub-district codes, 167 ms with the index.
        //
        // No INCLUDE — the covering column (AppraisalPropertyId) belongs to the owner entity and
        // cannot be included from an owned-type builder, and measuring it made no material
        // difference.
        addr.HasIndex(a => a.Province);
        addr.HasIndex(a => a.District);
        addr.HasIndex(a => a.SubDistrict);
    }

    /// <summary>
    /// The administrative address (DOPA), stored alongside in Dopa-prefixed columns. Its indexes
    /// are filtered: the DOPA address is populated on a small minority of rows, so an unfiltered
    /// index would be almost entirely NULL keys.
    /// </summary>
    public static void ConfigureDopaAddress<TOwner>(OwnedNavigationBuilder<TOwner, Address> addr)
        where TOwner : class
    {
        addr.Property(a => a.SubDistrict).HasColumnName("DopaSubDistrict").HasMaxLength(100);
        addr.Property(a => a.District).HasColumnName("DopaDistrict").HasMaxLength(100);
        addr.Property(a => a.Province).HasColumnName("DopaProvince").HasMaxLength(100);

        addr.HasIndex(a => a.Province).HasFilter("[DopaProvince] IS NOT NULL");
        addr.HasIndex(a => a.District).HasFilter("[DopaDistrict] IS NOT NULL");
        addr.HasIndex(a => a.SubDistrict).HasFilter("[DopaSubDistrict] IS NOT NULL");
    }
}
