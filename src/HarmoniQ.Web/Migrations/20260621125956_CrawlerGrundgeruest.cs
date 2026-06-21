using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class CrawlerGrundgeruest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrawlQuellen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Typ = table.Column<int>(type: "integer", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartUrl = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: true),
                    BrauchtRendering = table.Column<bool>(type: "boolean", nullable: false),
                    MaxTiefe = table.Column<int>(type: "integer", nullable: false),
                    MaxSeiten = table.Column<int>(type: "integer", nullable: false),
                    Aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LetzterLaufAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlQuellen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrawlQuellen_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CrawlLaeufe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuelleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndeAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SeitenBesucht = table.Column<int>(type: "integer", nullable: false),
                    FundeAnzahl = table.Column<int>(type: "integer", nullable: false),
                    Meldung = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlLaeufe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrawlLaeufe_CrawlQuellen_QuelleId",
                        column: x => x.QuelleId,
                        principalTable: "CrawlQuellen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrawlSeiten",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuelleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    InhaltsHash = table.Column<string>(type: "text", nullable: true),
                    AbgerufenAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Relevant = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlSeiten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrawlSeiten_CrawlQuellen_QuelleId",
                        column: x => x.QuelleId,
                        principalTable: "CrawlQuellen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrawlFunde",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaufId = table.Column<Guid>(type: "uuid", nullable: false),
                    Typ = table.Column<int>(type: "integer", nullable: false),
                    QuellUrl = table.Column<string>(type: "text", nullable: false),
                    AbgerufenAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatenJson = table.Column<string>(type: "text", nullable: false),
                    Konfidenz = table.Column<int>(type: "integer", nullable: true),
                    DublettHinweis = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EntschiedenAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlFunde", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrawlFunde_CrawlLaeufe_LaufId",
                        column: x => x.LaufId,
                        principalTable: "CrawlLaeufe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrawlFunde_LaufId",
                table: "CrawlFunde",
                column: "LaufId");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlFunde_Status",
                table: "CrawlFunde",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlFunde_Typ",
                table: "CrawlFunde",
                column: "Typ");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlLaeufe_QuelleId",
                table: "CrawlLaeufe",
                column: "QuelleId");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlLaeufe_StartAm",
                table: "CrawlLaeufe",
                column: "StartAm");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlLaeufe_Status",
                table: "CrawlLaeufe",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlQuellen_Aktiv",
                table: "CrawlQuellen",
                column: "Aktiv");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlQuellen_BandId",
                table: "CrawlQuellen",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlSeiten_QuelleId_Url",
                table: "CrawlSeiten",
                columns: new[] { "QuelleId", "Url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrawlFunde");

            migrationBuilder.DropTable(
                name: "CrawlSeiten");

            migrationBuilder.DropTable(
                name: "CrawlLaeufe");

            migrationBuilder.DropTable(
                name: "CrawlQuellen");
        }
    }
}
