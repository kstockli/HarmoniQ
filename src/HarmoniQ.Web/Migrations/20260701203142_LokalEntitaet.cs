using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class LokalEntitaet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LokalId",
                table: "Konzerte",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Lokale",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Saal = table.Column<string>(type: "text", nullable: true),
                    Adresse = table.Column<string>(type: "text", nullable: true),
                    Stadt = table.Column<string>(type: "text", nullable: true),
                    Kanton = table.Column<string>(type: "text", nullable: true),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    Lng = table.Column<double>(type: "double precision", nullable: true),
                    Webseite = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lokale", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LokalAliase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LokalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LokalAliase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LokalAliase_Lokale_LokalId",
                        column: x => x.LokalId,
                        principalTable: "Lokale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Konzerte_LokalId",
                table: "Konzerte",
                column: "LokalId");

            migrationBuilder.CreateIndex(
                name: "IX_LokalAliase_LokalId_Name",
                table: "LokalAliase",
                columns: new[] { "LokalId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lokale_Kanton",
                table: "Lokale",
                column: "Kanton");

            migrationBuilder.CreateIndex(
                name: "IX_Lokale_Name",
                table: "Lokale",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Konzerte_Lokale_LokalId",
                table: "Konzerte",
                column: "LokalId",
                principalTable: "Lokale",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Konzerte_Lokale_LokalId",
                table: "Konzerte");

            migrationBuilder.DropTable(
                name: "LokalAliase");

            migrationBuilder.DropTable(
                name: "Lokale");

            migrationBuilder.DropIndex(
                name: "IX_Konzerte_LokalId",
                table: "Konzerte");

            migrationBuilder.DropColumn(
                name: "LokalId",
                table: "Konzerte");
        }
    }
}
