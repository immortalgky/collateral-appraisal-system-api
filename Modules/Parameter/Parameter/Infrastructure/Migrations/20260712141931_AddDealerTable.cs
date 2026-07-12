using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parameter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dealers",
                schema: "parameter",
                columns: table => new
                {
                    DealerCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DealerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dealers", x => x.DealerCode);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dealers",
                schema: "parameter");
        }
    }
}
