using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class InstrumenteUndMitwirkungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Instrumente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instrumente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonInstrumente",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonInstrumente", x => new { x.PersonId, x.InstrumentId });
                    table.ForeignKey(
                        name: "FK_PersonInstrumente_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonInstrumente_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stimmen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stimmen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stimmen_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoMitwirkungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VideoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rolle = table.Column<int>(type: "INTEGER", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StimmeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Anmerkung = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VorgeschlagenVonId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoMitwirkungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_AspNetUsers_VorgeschlagenVonId",
                        column: x => x.VorgeschlagenVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_Stimmen_StimmeId",
                        column: x => x.StimmeId,
                        principalTable: "Stimmen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Instrumente_Name",
                table: "Instrumente",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonInstrumente_InstrumentId",
                table: "PersonInstrumente",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Stimmen_InstrumentId_Bezeichnung",
                table: "Stimmen",
                columns: new[] { "InstrumentId", "Bezeichnung" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_InstrumentId",
                table: "VideoMitwirkungen",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_PersonId",
                table: "VideoMitwirkungen",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_StimmeId",
                table: "VideoMitwirkungen",
                column: "StimmeId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_VideoId",
                table: "VideoMitwirkungen",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_VorgeschlagenVonId",
                table: "VideoMitwirkungen",
                column: "VorgeschlagenVonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonInstrumente");

            migrationBuilder.DropTable(
                name: "VideoMitwirkungen");

            migrationBuilder.DropTable(
                name: "Stimmen");

            migrationBuilder.DropTable(
                name: "Instrumente");
        }
    }
}
