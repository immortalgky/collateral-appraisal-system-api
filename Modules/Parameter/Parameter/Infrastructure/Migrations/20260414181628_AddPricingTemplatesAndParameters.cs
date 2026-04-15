using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parameter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingTemplatesAndParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingParameterAssumptionMethods",
                schema: "parameter",
                columns: table => new
                {
                    AssumptionType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MethodTypeCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingParameterAssumptionMethods", x => new { x.AssumptionType, x.MethodTypeCode });
                });

            migrationBuilder.CreateTable(
                name: "PricingParameterAssumptionTypes",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingParameterAssumptionTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "PricingParameterJobPositions",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingParameterJobPositions", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "PricingParameterRoomTypes",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingParameterRoomTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "PricingParameterTaxBrackets",
                schema: "parameter",
                columns: table => new
                {
                    Tier = table.Column<int>(type: "int", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    MinValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingParameterTaxBrackets", x => x.Tier);
                });

            migrationBuilder.CreateTable(
                name: "PricingTemplates",
                schema: "parameter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TemplateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalNumberOfYears = table.Column<int>(type: "int", nullable: false),
                    TotalNumberOfDayInYear = table.Column<int>(type: "int", nullable: false, defaultValue: 365),
                    CapitalizeRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DiscountedRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingTemplateSections",
                schema: "parameter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingTemplateSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingTemplateSections_PricingTemplates_PricingTemplateId",
                        column: x => x.PricingTemplateId,
                        principalSchema: "parameter",
                        principalTable: "PricingTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingTemplateCategories",
                schema: "parameter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingTemplateSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingTemplateCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingTemplateCategories_PricingTemplateSections_PricingTemplateSectionId",
                        column: x => x.PricingTemplateSectionId,
                        principalSchema: "parameter",
                        principalTable: "PricingTemplateSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingTemplateAssumptions",
                schema: "parameter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingTemplateCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssumptionType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AssumptionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false),
                    MethodTypeCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    MethodDetailJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingTemplateAssumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingTemplateAssumptions_PricingTemplateCategories_PricingTemplateCategoryId",
                        column: x => x.PricingTemplateCategoryId,
                        principalSchema: "parameter",
                        principalTable: "PricingTemplateCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingTemplateAssumptions_PricingTemplateCategoryId",
                schema: "parameter",
                table: "PricingTemplateAssumptions",
                column: "PricingTemplateCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingTemplateCategories_PricingTemplateSectionId",
                schema: "parameter",
                table: "PricingTemplateCategories",
                column: "PricingTemplateSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingTemplates_Code",
                schema: "parameter",
                table: "PricingTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingTemplateSections_PricingTemplateId",
                schema: "parameter",
                table: "PricingTemplateSections",
                column: "PricingTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingParameterAssumptionMethods",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "PricingParameterAssumptionTypes",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "PricingParameterJobPositions",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "PricingParameterRoomTypes",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "PricingParameterTaxBrackets",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "PricingTemplateAssumptions",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "PricingTemplateCategories",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "PricingTemplateSections",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "PricingTemplates",
                schema: "parameter");
        }
    }
}
