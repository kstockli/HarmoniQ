using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class VideoPlattformUndKonzertRang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "YouTubeVideoId",
                table: "Videos",
                newName: "ExternId");

            migrationBuilder.AddColumn<int>(
                name: "Plattform",
                table: "Videos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Punkte",
                table: "KonzertBands",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rang",
                table: "KonzertBands",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plattform",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Punkte",
                table: "KonzertBands");

            migrationBuilder.DropColumn(
                name: "Rang",
                table: "KonzertBands");

            migrationBuilder.RenameColumn(
                name: "ExternId",
                table: "Videos",
                newName: "YouTubeVideoId");
        }
    }
}
