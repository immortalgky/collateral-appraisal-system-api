using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parameter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DopaProvinces",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NameTh = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DopaProvinces", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "TitleProvinces",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NameTh = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitleProvinces", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "DopaDistricts",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    NameTh = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DopaDistricts", x => x.Code);
                    table.ForeignKey(
                        name: "FK_DopaDistricts_DopaProvinces_ProvinceCode",
                        column: x => x.ProvinceCode,
                        principalSchema: "parameter",
                        principalTable: "DopaProvinces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TitleDistricts",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    NameTh = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitleDistricts", x => x.Code);
                    table.ForeignKey(
                        name: "FK_TitleDistricts_TitleProvinces_ProvinceCode",
                        column: x => x.ProvinceCode,
                        principalSchema: "parameter",
                        principalTable: "TitleProvinces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DopaSubDistricts",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    NameTh = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DistrictCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Postcode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DopaSubDistricts", x => x.Code);
                    table.ForeignKey(
                        name: "FK_DopaSubDistricts_DopaDistricts_DistrictCode",
                        column: x => x.DistrictCode,
                        principalSchema: "parameter",
                        principalTable: "DopaDistricts",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TitleSubDistricts",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    NameTh = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DistrictCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Postcode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitleSubDistricts", x => x.Code);
                    table.ForeignKey(
                        name: "FK_TitleSubDistricts_TitleDistricts_DistrictCode",
                        column: x => x.DistrictCode,
                        principalSchema: "parameter",
                        principalTable: "TitleDistricts",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DopaDistricts_ProvinceCode",
                schema: "parameter",
                table: "DopaDistricts",
                column: "ProvinceCode");

            migrationBuilder.CreateIndex(
                name: "IX_DopaSubDistricts_DistrictCode",
                schema: "parameter",
                table: "DopaSubDistricts",
                column: "DistrictCode");

            migrationBuilder.CreateIndex(
                name: "IX_TitleDistricts_ProvinceCode",
                schema: "parameter",
                table: "TitleDistricts",
                column: "ProvinceCode");

            migrationBuilder.CreateIndex(
                name: "IX_TitleSubDistricts_DistrictCode",
                schema: "parameter",
                table: "TitleSubDistricts",
                column: "DistrictCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DopaSubDistricts",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "TitleSubDistricts",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "DopaDistricts",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "TitleDistricts",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "DopaProvinces",
                schema: "parameter");

            migrationBuilder.DropTable(
                name: "TitleProvinces",
                schema: "parameter");
        }
    }
}
