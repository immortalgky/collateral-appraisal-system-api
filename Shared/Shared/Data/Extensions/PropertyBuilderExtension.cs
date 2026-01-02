using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Data.Extensions;

public static class PropertyBuilderExtension
{
    // Username
    public static PropertyBuilder<string> UseUserNameConfig(this PropertyBuilder<string> builder)
    {
        return builder.HasMaxLength(10).IsRequired();
    }

    // Code
    public static PropertyBuilder<T> UseCodeConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasColumnType("varchar(10)").HasMaxLength(10);
    }

    // Remark
    public static PropertyBuilder<T> UseRemarkConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasColumnType("nvarchar(max)");
    }

    // Money
    public static PropertyBuilder<decimal> UseMoneyConfig(this PropertyBuilder<decimal> builder)
    {
        return builder.HasPrecision(19, 4);
    }

    // Money Nullable
    public static PropertyBuilder<decimal?> UseMoneyConfig(this PropertyBuilder<decimal?> builder)
    {
        return builder.HasPrecision(19, 4);
    }

    public static PropertyBuilder<T> UseMaxLengthNVarcharConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasColumnType("nvarchar(max)");
    }

    public static PropertyBuilder<T> UseTinyStringConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(10);
    }

    public static PropertyBuilder<T> UseVeryShortStringConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(20);
    }

    public static PropertyBuilder<T> UseShortStringConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(25);
    }

    public static PropertyBuilder<T> UseMediumStringConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(50);
    }

    public static PropertyBuilder<T> UseLongStringConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(100);
    }

    public static PropertyBuilder<T> UseLongerStringConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(200);
    }

    public static PropertyBuilder<string?> UseNameConfig(this PropertyBuilder<string?> builder)
    {
        return builder.HasMaxLength(100);
    }

    public static PropertyBuilder<T> UseBuildingNoConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(10);
    }

    public static PropertyBuilder<T> UseBuildOnTitleNoConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(10);
    }

    public static PropertyBuilder<T> UseVillageConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(100);
    }

    public static PropertyBuilder<T> UseSoiConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(100);
    }

    public static PropertyBuilder<T> UseStreetConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(100);
    }

    public static PropertyBuilder<T> UseSubDistrictConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(50);
    }

    public static PropertyBuilder<T> UseDistrictConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(50);
    }

    public static PropertyBuilder<T> UseProvinceConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(50);
    }

    public static PropertyBuilder<T> UseLandOfficeConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(50);
    }

    public static PropertyBuilder<T> UseMultiSelectConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(100);
    }

    public static PropertyBuilder<T> UseObligationConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasColumnType("nvarchar(max)");
    }

    public static PropertyBuilder<T> UseOtherFieldConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(100);
    }

    public static PropertyBuilder<T> UseRoyalDecreeConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasMaxLength(20);
    }

    public static PropertyBuilder<T> UseDistanceConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(5, 2);
    }

    public static PropertyBuilder<T> UseMeasurementConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(5, 2);
    }

    public static PropertyBuilder<T> UseAreaConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(5, 2);
    }

    public static PropertyBuilder<T> UseLargeAreaConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(19, 4);
    }

    public static PropertyBuilder<decimal?> UseSizeConfig(this PropertyBuilder<decimal?> builder)
    {
        return builder.HasPrecision(7, 2);
    }

    public static PropertyBuilder<decimal> UseLatLonConfig(this PropertyBuilder<decimal> builder)
    {
        return builder.HasPrecision(9, 6);
    }

    public static PropertyBuilder<decimal?> UseLatLonConfig(this PropertyBuilder<decimal?> builder)
    {
        return builder.HasPrecision(9, 6);
    }

    public static PropertyBuilder<decimal> UseCompactPriceConfig(this PropertyBuilder<decimal> builder)
    {
        return builder.HasPrecision(10, 2);
    }

    public static PropertyBuilder<decimal?> UseCompactPriceConfig(this PropertyBuilder<decimal?> builder)
    {
        return builder.HasPrecision(10, 2);
    }

    public static PropertyBuilder<T> UseRaiConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(3, 0);
    }

    public static PropertyBuilder<T> UseNganConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(5, 0);
    }

    public static PropertyBuilder<T> UseWaConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(4, 2);
    }

    public static PropertyBuilder<T> UseRightOfWayConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(5, 2);
    }

    public static PropertyBuilder<T> UseRoadWidthConfig<T>(this PropertyBuilder<T> builder)
    {
        return builder.HasPrecision(5, 2);
    }
}