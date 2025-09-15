using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaBotApi.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherToSoilData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Humidity",
                table: "SoilDatas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Temperature",
                table: "SoilDatas",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Humidity",
                table: "SoilDatas");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "SoilDatas");
        }
    }
}
