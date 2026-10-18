using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddReappraisalCandidateIsBlockUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBlockUnit",
                schema: "collateral",
                table: "ReappraisalCandidates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBlockUnit",
                schema: "collateral",
                table: "ReappraisalCandidates");
        }
    }
}
