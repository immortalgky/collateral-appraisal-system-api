using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketComparableTemplateSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketComparableFactors",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    FactorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FactorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FieldLength = table.Column<int>(type: "int", nullable: true),
                    FieldDecimal = table.Column<int>(type: "int", nullable: true),
                    ParameterGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableFactors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableImages",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MarketComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketComparableImages_MarketComparables_MarketComparableId",
                        column: x => x.MarketComparableId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableTemplates",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableData",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MarketComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtherRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketComparableData_MarketComparableFactors_FactorId",
                        column: x => x.FactorId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparableFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketComparableData_MarketComparables_MarketComparableId",
                        column: x => x.MarketComparableId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableTemplateFactors",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableTemplateFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketComparableTemplateFactors_MarketComparableFactors_FactorId",
                        column: x => x.FactorId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparableFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketComparableTemplateFactors_MarketComparableTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparableTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableData_FactorId",
                schema: "appraisal",
                table: "MarketComparableData",
                column: "FactorId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableData_MarketComparableId",
                schema: "appraisal",
                table: "MarketComparableData",
                column: "MarketComparableId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableData_MarketComparableId_FactorId",
                schema: "appraisal",
                table: "MarketComparableData",
                columns: new[] { "MarketComparableId", "FactorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableFactors_FactorCode",
                schema: "appraisal",
                table: "MarketComparableFactors",
                column: "FactorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableFactors_IsActive",
                schema: "appraisal",
                table: "MarketComparableFactors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableImages_MarketComparableId",
                schema: "appraisal",
                table: "MarketComparableImages",
                column: "MarketComparableId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableImages_MarketComparableId_DisplaySequence",
                schema: "appraisal",
                table: "MarketComparableImages",
                columns: new[] { "MarketComparableId", "DisplaySequence" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplateFactors_FactorId",
                schema: "appraisal",
                table: "MarketComparableTemplateFactors",
                column: "FactorId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplateFactors_TemplateId",
                schema: "appraisal",
                table: "MarketComparableTemplateFactors",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplateFactors_TemplateId_FactorId",
                schema: "appraisal",
                table: "MarketComparableTemplateFactors",
                columns: new[] { "TemplateId", "FactorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplates_IsActive",
                schema: "appraisal",
                table: "MarketComparableTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplates_PropertyType",
                schema: "appraisal",
                table: "MarketComparableTemplates",
                column: "PropertyType");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplates_TemplateCode",
                schema: "appraisal",
                table: "MarketComparableTemplates",
                column: "TemplateCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketComparableData",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableImages",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableTemplateFactors",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableFactors",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableTemplates",
                schema: "appraisal");
        }
    }
}
