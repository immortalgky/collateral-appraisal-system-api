using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTowerModelTypeIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelTypeIds",
                schema: "appraisal",
                table: "ProjectTowers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModelTypeIds",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(2000)",
                nullable: true);
        }
    }
}
