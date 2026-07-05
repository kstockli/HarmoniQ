using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class PersonHeimatStandort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeimatPlz",
                table: "Personen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StandortLat",
                table: "Personen",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StandortLng",
                table: "Personen",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeimatPlz",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "StandortLat",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "StandortLng",
                table: "Personen");
        }
    }
}
