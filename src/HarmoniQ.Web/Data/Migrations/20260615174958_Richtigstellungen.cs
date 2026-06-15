using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class Richtigstellungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Richtigstellungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BetrifftTyp = table.Column<int>(type: "INTEGER", nullable: false),
                    BetrifftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    EingereichtVonId = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Antwort = table.Column<string>(type: "TEXT", nullable: true),
                    AntwortAm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Richtigstellungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Richtigstellungen_AspNetUsers_EingereichtVonId",
                        column: x => x.EingereichtVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Richtigstellungen_BetrifftTyp_BetrifftId",
                table: "Richtigstellungen",
                columns: new[] { "BetrifftTyp", "BetrifftId" });

            migrationBuilder.CreateIndex(
                name: "IX_Richtigstellungen_EingereichtVonId",
                table: "Richtigstellungen",
                column: "EingereichtVonId");

            migrationBuilder.CreateIndex(
                name: "IX_Richtigstellungen_Status",
                table: "Richtigstellungen",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Richtigstellungen");
        }
    }
}
