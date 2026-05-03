using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHypothesisAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HypothesisAnalyses",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Variant = table.Column<int>(type: "int", nullable: false),
                    LandBuildingSummary_TotalArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    LandBuildingSummary_SellingAreaPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_SellingArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    LandBuildingSummary_PublicUtilityAreaPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_PublicUtilityArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    LandBuildingSummary_TotalRevenue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_EstSalesPeriod = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_TotalUnits = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_EstimatedDurationMonths = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_PublicUtilityRatePerSqWa = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_PublicUtilityAreaForCost = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    LandBuildingSummary_PublicUtilityCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_PublicUtilityCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_LandFillingRatePerSqWa = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_LandFillingArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    LandBuildingSummary_LandFillingCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_LandFillingCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_ContingencyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_ContingencyAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_ContingencyRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_TotalProjectDevCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_TotalDevCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_EstConstructionPeriod = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_TotalUnitsForConstruction = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_EstimatedConstructionDurationMonths = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_AllocationPermitFee = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_AllocationPermitFeeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_LandTitleFeePerPlot = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_TotalPlots = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_LandTitleFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_LandTitleFeeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_ProfessionalFeePerMonth = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_ProfessionalFeeMonths = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_ProfessionalFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_ProfessionalFeeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_AdminCostPerMonth = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_AdminCostMonths = table.Column<int>(type: "int", nullable: true),
                    LandBuildingSummary_AdminCostTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_AdminCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_SellingAdvPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_SellingAdvTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_SellingAdvRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_ProjectContingencyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_ProjectContingencyAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_ProjectContingencyRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_TotalProjectCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_TotalProjectCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_TransferFeePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_TransferFeeAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_TransferFeeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_SpecificBizTaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_SpecificBizTaxAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_SpecificBizTaxRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_TotalGovTax = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_TotalGovTaxRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_RiskPremiumPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_RiskPremiumAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_TotalDevCostsAndExpenses = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_CurrentPropertyValue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_DiscountRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandBuildingSummary_DiscountRateFactor = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: true),
                    LandBuildingSummary_FinalPropertyValue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_TotalAssetValueRounded = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_TotalAssetValuePerSqWa = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LandBuildingSummary_Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CondominiumSummary_AreaTitleDeed = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_AreaSqM = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_FAR = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_ConstructionAreaCityPlan = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_TotalBuildingArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_CommonAreaPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_CommonArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_IndoorSalesAreaPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_IndoorSalesArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_ProjectSalesArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_AveragePricePerSqM = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalProjectSellingPrice = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalRevenue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_EstSalesDurationMonths = table.Column<int>(type: "int", nullable: true),
                    CondominiumSummary_CondoBuildingCostPerSqM = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_BuildingArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_CondoBuildingCostTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_SetAvgRoomSizeUnits = table.Column<int>(type: "int", nullable: true),
                    CondominiumSummary_AvgIndoorSalesAreaPerUnit = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CondominiumSummary_FurniturePerUnit = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_FurnitureQuantity = table.Column<int>(type: "int", nullable: true),
                    CondominiumSummary_FurnitureTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_ExternalUtilities = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_ExternalUtilitiesTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_HardCostContingencyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_HardCostContingencyAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalHardCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_EstConstructionPeriodMonths = table.Column<int>(type: "int", nullable: true),
                    CondominiumSummary_ProfessionalFeePerMonth = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_ProfessionalFeeMonths = table.Column<int>(type: "int", nullable: true),
                    CondominiumSummary_ProfessionalFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_AdminCostPerMonth = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_AdminCostMonths = table.Column<int>(type: "int", nullable: true),
                    CondominiumSummary_AdminCostTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_SellingAdvPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_SellingAdvTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TitleDeedFee = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TitleDeedFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_EIACost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_EIACostTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_CondoRegistrationFee = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_CondoRegistrationFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_OtherExpensesPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_OtherExpensesTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalSoftCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TransferFeePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_TransferFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_SpecificBizTaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_SpecificBizTaxTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalGovTax = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_RiskProfitPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_RiskProfitTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalDevCosts = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalRemainingValue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_DiscountRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondominiumSummary_DiscountRateFactor = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: true),
                    CondominiumSummary_FinalRemainingValue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalAssetValueRounded = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_TotalAssetValuePerSqM = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CondominiumSummary_Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HypothesisAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HypothesisAnalyses_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HypothesisCondominiumUnitRows",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HypothesisAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    FloorNo = table.Column<int>(type: "int", nullable: true),
                    Building = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AptNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UsableAreaSqM = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HypothesisCondominiumUnitRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HypothesisCondominiumUnitRows_HypothesisAnalyses_HypothesisAnalysisId",
                        column: x => x.HypothesisAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "HypothesisAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HypothesisCostItems",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    HypothesisAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    RateAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: false),
                    RatePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CategoryRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HypothesisCostItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HypothesisCostItems_HypothesisAnalyses_HypothesisAnalysisId",
                        column: x => x.HypothesisAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "HypothesisAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HypothesisLandBuildingUnitRows",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HypothesisAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    PlanNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandAreaSqWa = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HypothesisLandBuildingUnitRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HypothesisLandBuildingUnitRows_HypothesisAnalyses_HypothesisAnalysisId",
                        column: x => x.HypothesisAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "HypothesisAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HypothesisUnitDetailUploads",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    HypothesisAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HypothesisUnitDetailUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HypothesisUnitDetailUploads_HypothesisAnalyses_HypothesisAnalysisId",
                        column: x => x.HypothesisAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "HypothesisAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisAnalyses_PricingMethodId",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                column: "PricingMethodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisCondominiumUnitRows_HypothesisAnalysisId",
                schema: "appraisal",
                table: "HypothesisCondominiumUnitRows",
                column: "HypothesisAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisCondominiumUnitRows_UploadId",
                schema: "appraisal",
                table: "HypothesisCondominiumUnitRows",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisCostItems_HypothesisAnalysisId",
                schema: "appraisal",
                table: "HypothesisCostItems",
                column: "HypothesisAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisCostItems_HypothesisAnalysisId_Category",
                schema: "appraisal",
                table: "HypothesisCostItems",
                columns: new[] { "HypothesisAnalysisId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisLandBuildingUnitRows_HypothesisAnalysisId",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows",
                column: "HypothesisAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisLandBuildingUnitRows_UploadId",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisUnitDetailUploads_HypothesisAnalysisId",
                schema: "appraisal",
                table: "HypothesisUnitDetailUploads",
                column: "HypothesisAnalysisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HypothesisCondominiumUnitRows",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "HypothesisCostItems",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "HypothesisLandBuildingUnitRows",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "HypothesisUnitDetailUploads",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "HypothesisAnalyses",
                schema: "appraisal");
        }
    }
}
