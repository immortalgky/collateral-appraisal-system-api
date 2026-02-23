using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCondoAppraisalAreaDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AppraisalPropertyId",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                newName: "CondoAppraisalDetailsId");

            migrationBuilder.RenameIndex(
                name: "IX_CondoAppraisalAreaDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                newName: "IX_CondoAppraisalAreaDetails_CondoAppraisalDetailsId");

            migrationBuilder.AlterColumn<decimal>(
                name: "AreaSize",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "AreaDescription",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AddForeignKey(
                name: "FK_CondoAppraisalAreaDetails_CondoAppraisalDetails_CondoAppraisalDetailsId",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                column: "CondoAppraisalDetailsId",
                principalSchema: "appraisal",
                principalTable: "CondoAppraisalDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CondoAppraisalAreaDetails_CondoAppraisalDetails_CondoAppraisalDetailsId",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails");

            migrationBuilder.RenameColumn(
                name: "CondoAppraisalDetailsId",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                newName: "AppraisalPropertyId");

            migrationBuilder.RenameIndex(
                name: "IX_CondoAppraisalAreaDetails_CondoAppraisalDetailsId",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                newName: "IX_CondoAppraisalAreaDetails_AppraisalPropertyId");

            migrationBuilder.AlterColumn<decimal>(
                name: "AreaSize",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AreaDescription",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
