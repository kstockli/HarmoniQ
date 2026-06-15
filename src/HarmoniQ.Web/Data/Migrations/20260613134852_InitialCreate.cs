using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Land = table.Column<string>(type: "TEXT", nullable: true),
                    Webseite = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Komponisten",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Biografie = table.Column<string>(type: "TEXT", nullable: true),
                    Webseite = table.Column<string>(type: "TEXT", nullable: true),
                    BildUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Komponisten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stuecke",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KomponistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Titel = table.Column<string>(type: "TEXT", nullable: false),
                    Jahr = table.Column<int>(type: "INTEGER", nullable: true),
                    Schwierigkeitsgrad = table.Column<int>(type: "INTEGER", nullable: false),
                    Besetzung = table.Column<string>(type: "TEXT", nullable: true),
                    Beschreibung = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stuecke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stuecke_Komponisten_KomponistId",
                        column: x => x.KomponistId,
                        principalTable: "Komponisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StueckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BandId = table.Column<Guid>(type: "TEXT", nullable: true),
                    YouTubeVideoId = table.Column<string>(type: "TEXT", nullable: false),
                    Titel = table.Column<string>(type: "TEXT", nullable: false),
                    AufnahmeDatum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VorgeschlagenVonId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Videos_AspNetUsers_VorgeschlagenVonId",
                        column: x => x.VorgeschlagenVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Videos_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Videos_Stuecke_StueckId",
                        column: x => x.StueckId,
                        principalTable: "Stuecke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bewertungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VideoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BenutzerId = table.Column<string>(type: "TEXT", nullable: true),
                    AnonymerCookieId = table.Column<string>(type: "TEXT", nullable: true),
                    GesamtEindruck = table.Column<int>(type: "INTEGER", nullable: false),
                    Praezision = table.Column<int>(type: "INTEGER", nullable: false),
                    Musikalitaet = table.Column<int>(type: "INTEGER", nullable: false),
                    AkustischeQualitaet = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoQualitaet = table.Column<int>(type: "INTEGER", nullable: false),
                    Kommentar = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bewertungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bewertungen_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bewertungen_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bewertungen_BenutzerId",
                table: "Bewertungen",
                column: "BenutzerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bewertungen_VideoId_AnonymerCookieId",
                table: "Bewertungen",
                columns: new[] { "VideoId", "AnonymerCookieId" },
                unique: true,
                filter: "[AnonymerCookieId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bewertungen_VideoId_BenutzerId",
                table: "Bewertungen",
                columns: new[] { "VideoId", "BenutzerId" },
                unique: true,
                filter: "[BenutzerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stuecke_KomponistId",
                table: "Stuecke",
                column: "KomponistId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_BandId",
                table: "Videos",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_StueckId",
                table: "Videos",
                column: "StueckId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_VorgeschlagenVonId",
                table: "Videos",
                column: "VorgeschlagenVonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bewertungen");

            migrationBuilder.DropTable(
                name: "Videos");

            migrationBuilder.DropTable(
                name: "Bands");

            migrationBuilder.DropTable(
                name: "Stuecke");

            migrationBuilder.DropTable(
                name: "Komponisten");
        }
    }
}
