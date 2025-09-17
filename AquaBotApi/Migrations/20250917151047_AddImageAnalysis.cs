using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaBotApi.Migrations
{
    /// <inheritdoc />
    public partial class AddImageAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageAnalysisResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SoilCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedMoisture = table.Column<int>(type: "int", nullable: false),
                    CropHealth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CropType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AvgBrightness = table.Column<double>(type: "float", nullable: false),
                    GreenPercentage = table.Column<double>(type: "float", nullable: false),
                    BrownPercentage = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageAnalysisResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageAnalysisResults_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageAnalysisResults_UserId",
                table: "ImageAnalysisResults",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageAnalysisResults");
        }
    }
}
