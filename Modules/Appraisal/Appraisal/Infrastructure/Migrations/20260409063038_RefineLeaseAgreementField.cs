using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefineLeaseAgreementField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Banking",
                schema: "appraisal",
                table: "LeaseAgreementDetails");

            migrationBuilder.RenameColumn(
                name: "TenantName",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                newName: "LessorName");

            migrationBuilder.RenameColumn(
                name: "LeaseTimestamp",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                newName: "LeaseTerminate");

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RemainingLeaseAsAppraisalDate",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "decimal(5,0)",
                precision: 5,
                scale: 0,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "LeasePeriodAsContract",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "decimal(5,0)",
                precision: 5,
                scale: 0,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AdditionalExpenses",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LessorName",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                newName: "TenantName");

            migrationBuilder.RenameColumn(
                name: "LeaseTerminate",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                newName: "LeaseTimestamp");

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RemainingLeaseAsAppraisalDate",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,0)",
                oldPrecision: 5,
                oldScale: 0,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LeasePeriodAsContract",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,0)",
                oldPrecision: 5,
                oldScale: 0,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AdditionalExpenses",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Banking",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
