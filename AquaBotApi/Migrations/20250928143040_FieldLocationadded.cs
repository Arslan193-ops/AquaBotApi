using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaBotApi.Migrations
{
    /// <inheritdoc />
    public partial class FieldLocationadded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FieldLocation",
                table: "ImageAnalysisResults",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldLocation",
                table: "ImageAnalysisResults");
        }
    }
}
