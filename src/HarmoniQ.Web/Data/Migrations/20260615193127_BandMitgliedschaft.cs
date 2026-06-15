using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BandMitgliedschaft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BandMitgliedschaften",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VonJahr = table.Column<int>(type: "INTEGER", nullable: true),
                    BisJahr = table.Column<int>(type: "INTEGER", nullable: true),
                    Funktion = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandMitgliedschaften", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandMitgliedschaften_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BandMitgliedschaften_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BandMitgliedschaften_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BandMitgliedschaften_BandId_PersonId",
                table: "BandMitgliedschaften",
                columns: new[] { "BandId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_BandMitgliedschaften_InstrumentId",
                table: "BandMitgliedschaften",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BandMitgliedschaften_PersonId",
                table: "BandMitgliedschaften",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BandMitgliedschaften");
        }
    }
}
