using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseAgreementPropertyTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaseAgreementDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LesseeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TenantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeasePeriodAsContract = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RemainingLeaseAsAppraisalDate = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeaseStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseRentFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RentAdjust = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Sublease = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdditionalExpenses = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LeaseTimestamp = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractRenewal = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RentalTermsImpactingPropertyUse = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TerminationOfLease = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Banking = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseAgreementDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseAgreementDetails_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalInfos",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumberOfYears = table.Column<int>(type: "int", nullable: false),
                    FirstYearStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractRentalFeePerYear = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpFrontTotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrowthRateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    GrowthRatePercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    GrowthIntervalYears = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalInfos_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalGrowthPeriodEntries",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    RentalInfoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromYear = table.Column<int>(type: "int", nullable: false),
                    ToYear = table.Column<int>(type: "int", nullable: false),
                    GrowthRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    GrowthAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalGrowthPeriodEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalGrowthPeriodEntries_RentalInfos_RentalInfoId",
                        column: x => x.RentalInfoId,
                        principalSchema: "appraisal",
                        principalTable: "RentalInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalUpFrontEntries",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    RentalInfoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtYear = table.Column<int>(type: "int", nullable: false),
                    UpFrontAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalUpFrontEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalUpFrontEntries_RentalInfos_RentalInfoId",
                        column: x => x.RentalInfoId,
                        principalSchema: "appraisal",
                        principalTable: "RentalInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaseAgreementDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                column: "AppraisalPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalGrowthPeriodEntries_RentalInfoId",
                schema: "appraisal",
                table: "RentalGrowthPeriodEntries",
                column: "RentalInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalInfos_AppraisalPropertyId",
                schema: "appraisal",
                table: "RentalInfos",
                column: "AppraisalPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalUpFrontEntries_RentalInfoId",
                schema: "appraisal",
                table: "RentalUpFrontEntries",
                column: "RentalInfoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseAgreementDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "RentalGrowthPeriodEntries",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "RentalUpFrontEntries",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "RentalInfos",
                schema: "appraisal");
        }
    }
}
