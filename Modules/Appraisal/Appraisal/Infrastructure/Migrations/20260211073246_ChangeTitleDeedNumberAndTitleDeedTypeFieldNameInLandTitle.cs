using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTitleDeedNumberAndTitleDeedTypeFieldNameInLandTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TitleDeedType",
                schema: "appraisal",
                table: "LandTitles",
                newName: "TitleType");

            migrationBuilder.RenameColumn(
                name: "TitleDeedNumber",
                schema: "appraisal",
                table: "LandTitles",
                newName: "TitleNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TitleType",
                schema: "appraisal",
                table: "LandTitles",
                newName: "TitleDeedType");

            migrationBuilder.RenameColumn(
                name: "TitleNumber",
                schema: "appraisal",
                table: "LandTitles",
                newName: "TitleDeedNumber");
        }
    }
}
