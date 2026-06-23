using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BandErweiterung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Geschichte",
                table: "Bands",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gruendungsjahr",
                table: "Bands",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kategorie",
                table: "Bands",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Staerkeklasse",
                table: "Bands",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BandAliase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandAliase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandAliase_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BandLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Typ = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandLinks_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BandAliase_BandId_Name",
                table: "BandAliase",
                columns: new[] { "BandId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BandLinks_BandId",
                table: "BandLinks",
                column: "BandId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BandAliase");

            migrationBuilder.DropTable(
                name: "BandLinks");

            migrationBuilder.DropColumn(
                name: "Geschichte",
                table: "Bands");

            migrationBuilder.DropColumn(
                name: "Gruendungsjahr",
                table: "Bands");

            migrationBuilder.DropColumn(
                name: "Kategorie",
                table: "Bands");

            migrationBuilder.DropColumn(
                name: "Staerkeklasse",
                table: "Bands");
        }
    }
}
