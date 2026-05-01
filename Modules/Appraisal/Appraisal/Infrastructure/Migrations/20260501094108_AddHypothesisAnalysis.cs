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
                    C01TotalArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    C02SellingAreaPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C03SellingArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    C10PublicUtilityAreaPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C10APublicUtilityArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    C15TotalRevenue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C16EstSalesPeriod = table.Column<int>(type: "int", nullable: true),
                    C17TotalUnits = table.Column<int>(type: "int", nullable: true),
                    C18EstimatedDurationMonths = table.Column<int>(type: "int", nullable: true),
                    C27PublicUtilityRatePerSqWa = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C28PublicUtilityArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    C29PublicUtilityCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C30PublicUtilityCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C31LandFillingRatePerSqWa = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C32LandFillingArea = table.Column<decimal>(type: "decimal(13,2)", precision: 13, scale: 2, nullable: true),
                    C33LandFillingCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C34LandFillingCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C35ContingencyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C36ContingencyAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C37ContingencyRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C38TotalProjectDevCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C39TotalDevCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C40EstConstructionPeriod = table.Column<int>(type: "int", nullable: true),
                    C41TotalUnits = table.Column<int>(type: "int", nullable: true),
                    C42EstimatedDurationMonths = table.Column<int>(type: "int", nullable: true),
                    C44AllocationPermitFee = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C45AllocationPermitFeeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C46LandTitleFeePerPlot = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C47TotalPlots = table.Column<int>(type: "int", nullable: true),
                    C48LandTitleFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C49LandTitleFeeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C50ProfessionalFeePerMonth = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C51ProfessionalFeeMonths = table.Column<int>(type: "int", nullable: true),
                    C52ProfessionalFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C53ProfessionalFeeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C54AdminCostPerMonth = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C55AdminCostMonths = table.Column<int>(type: "int", nullable: true),
                    C56AdminCostTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C57AdminCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C58SellingAdvPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C59SellingAdvTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C60SellingAdvRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C61ProjectContingencyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C62ProjectContingencyAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C63ProjectContingencyRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C64TotalProjectCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C65TotalProjectCostRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C66TransferFeePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C67TransferFeeAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C68TransferFeeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C69SpecificBizTaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C70SpecificBizTaxAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C71SpecificBizTaxRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C72TotalGovTax = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C73TotalGovTaxRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C74RiskPremiumPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C75RiskPremiumAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C76TotalDevCostsAndExpenses = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C77CurrentPropertyValue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C78DiscountRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    C79DiscountRateFactor = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: true),
                    C80FinalPropertyValue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C81TotalAssetValueRounded = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    C82TotalAssetValuePerSqWa = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    LB_Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    E01AreaTitleDeed = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E02AreaSqM = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E03FAR = table.Column<decimal>(type: "decimal(3,0)", precision: 3, scale: 0, nullable: true),
                    E04ConstructionAreaCityPlan = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E05TotalBuildingArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E06CommonAreaPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E07CommonArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E08IndoorSalesAreaPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E09IndoorSalesArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E10ProjectSalesArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E11AveragePricePerSqM = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E12TotalProjectSellingPrice = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E13TotalRevenue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E14EstSalesDurationMonths = table.Column<int>(type: "int", nullable: true),
                    E15CondoBuildingCostPerSqM = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E16BuildingArea = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E17CondoBuildingCostTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E18SetAvgRoomSizeUnits = table.Column<int>(type: "int", nullable: true),
                    E19AvgIndoorSalesAreaPerUnit = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    E20FurniturePerUnit = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E21FurnitureQuantity = table.Column<int>(type: "int", nullable: true),
                    E22FurnitureTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E23ExternalUtilities = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E24ExternalUtilitiesTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E25HardCostContingencyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E26HardCostContingencyAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E27TotalHardCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E28EstConstructionPeriodMonths = table.Column<int>(type: "int", nullable: true),
                    E29ProfessionalFeePerMonth = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E30ProfessionalFeeMonths = table.Column<int>(type: "int", nullable: true),
                    E31ProfessionalFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E32AdminCostPerMonth = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E33AdminCostMonths = table.Column<int>(type: "int", nullable: true),
                    E34AdminCostTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E35SellingAdvPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E36SellingAdvTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E37TitleDeedFee = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E38TitleDeedFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E39EIACost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E40EIACostTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E41CondoRegistrationFee = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E42CondoRegistrationFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E43OtherExpensesPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E44OtherExpensesTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E45TotalSoftCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E46TransferFeePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E47TransferFeeTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E48SpecificBizTaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E49SpecificBizTaxTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E50TotalGovTax = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E51RiskProfitPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E52RiskProfitTotal = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E53TotalDevCosts = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E54TotalRemainingValue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E55DiscountRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    E56DiscountRateFactor = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: true),
                    E57FinalRemainingValue = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E58TotalAssetValueRounded = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    E59TotalAssetValuePerSqM = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    Condo_Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
