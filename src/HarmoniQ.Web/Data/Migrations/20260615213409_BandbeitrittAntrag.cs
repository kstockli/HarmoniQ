using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BandbeitrittAntrag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BandbeitrittAntraege",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BeantragtVonId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntschiedenAm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandbeitrittAntraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandbeitrittAntraege_AspNetUsers_BeantragtVonId",
                        column: x => x.BeantragtVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BandbeitrittAntraege_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BandbeitrittAntraege_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BandbeitrittAntraege_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_BandId",
                table: "BandbeitrittAntraege",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_BeantragtVonId",
                table: "BandbeitrittAntraege",
                column: "BeantragtVonId");

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_InstrumentId",
                table: "BandbeitrittAntraege",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_PersonId",
                table: "BandbeitrittAntraege",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_Status",
                table: "BandbeitrittAntraege",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BandbeitrittAntraege");
        }
    }
}
