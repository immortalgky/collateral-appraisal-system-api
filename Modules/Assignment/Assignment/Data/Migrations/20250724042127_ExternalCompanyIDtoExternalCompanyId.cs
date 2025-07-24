using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExternalCompanyIDtoExternalCompanyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExternalCompanyID",
                schema: "assignment",
                table: "Assignments",
                newName: "ExternalCompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExternalCompanyId",
                schema: "assignment",
                table: "Assignments",
                newName: "ExternalCompanyID");
        }
    }
}
