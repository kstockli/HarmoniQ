using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class KonzertProgrammMitwirkende : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KonzertPersonen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KonzertId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rolle = table.Column<int>(type: "integer", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KonzertPersonen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KonzertPersonen_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KonzertPersonen_Konzerte_KonzertId",
                        column: x => x.KonzertId,
                        principalTable: "Konzerte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KonzertPersonen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KonzertStuecke",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KonzertId = table.Column<Guid>(type: "uuid", nullable: false),
                    StueckId = table.Column<Guid>(type: "uuid", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reihenfolge = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KonzertStuecke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KonzertStuecke_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KonzertStuecke_Konzerte_KonzertId",
                        column: x => x.KonzertId,
                        principalTable: "Konzerte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KonzertStuecke_Stuecke_StueckId",
                        column: x => x.StueckId,
                        principalTable: "Stuecke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KonzertPersonen_BandId",
                table: "KonzertPersonen",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_KonzertPersonen_KonzertId_PersonId_Rolle",
                table: "KonzertPersonen",
                columns: new[] { "KonzertId", "PersonId", "Rolle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KonzertPersonen_PersonId",
                table: "KonzertPersonen",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_KonzertStuecke_BandId",
                table: "KonzertStuecke",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_KonzertStuecke_KonzertId_StueckId_BandId",
                table: "KonzertStuecke",
                columns: new[] { "KonzertId", "StueckId", "BandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KonzertStuecke_StueckId",
                table: "KonzertStuecke",
                column: "StueckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KonzertPersonen");

            migrationBuilder.DropTable(
                name: "KonzertStuecke");
        }
    }
}
