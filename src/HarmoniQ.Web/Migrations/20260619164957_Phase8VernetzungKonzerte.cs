using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class Phase8VernetzungKonzerte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KonzertId",
                table: "Videos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BildUrl",
                table: "Bands",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Aktivitaeten",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AkteurPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Typ = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    ZielTyp = table.Column<int>(type: "integer", nullable: true),
                    ZielId = table.Column<Guid>(type: "uuid", nullable: true),
                    NebenPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Zeitpunkt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aktivitaeten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aktivitaeten_Personen_AkteurPersonId",
                        column: x => x.AkteurPersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Aktivitaeten_Personen_NebenPersonId",
                        column: x => x.NebenPersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Freundschaften",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnfragerPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpfaengerPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntschiedenAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Freundschaften", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Freundschaften_Personen_AnfragerPersonId",
                        column: x => x.AnfragerPersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Freundschaften_Personen_EmpfaengerPersonId",
                        column: x => x.EmpfaengerPersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Konzerte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Ort = table.Column<string>(type: "text", nullable: true),
                    Beschreibung = table.Column<string>(type: "text", nullable: true),
                    BildUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Konzerte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KonzertBands",
                columns: table => new
                {
                    KonzertId = table.Column<Guid>(type: "uuid", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KonzertBands", x => new { x.KonzertId, x.BandId });
                    table.ForeignKey(
                        name: "FK_KonzertBands_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KonzertBands_Konzerte_KonzertId",
                        column: x => x.KonzertId,
                        principalTable: "Konzerte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_KonzertId",
                table: "Videos",
                column: "KonzertId");

            migrationBuilder.CreateIndex(
                name: "IX_Aktivitaeten_AkteurPersonId",
                table: "Aktivitaeten",
                column: "AkteurPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Aktivitaeten_NebenPersonId",
                table: "Aktivitaeten",
                column: "NebenPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Aktivitaeten_Zeitpunkt",
                table: "Aktivitaeten",
                column: "Zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "IX_Freundschaften_AnfragerPersonId_EmpfaengerPersonId",
                table: "Freundschaften",
                columns: new[] { "AnfragerPersonId", "EmpfaengerPersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Freundschaften_EmpfaengerPersonId",
                table: "Freundschaften",
                column: "EmpfaengerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Freundschaften_Status",
                table: "Freundschaften",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KonzertBands_BandId",
                table: "KonzertBands",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_Konzerte_Datum",
                table: "Konzerte",
                column: "Datum");

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Konzerte_KonzertId",
                table: "Videos",
                column: "KonzertId",
                principalTable: "Konzerte",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Konzerte_KonzertId",
                table: "Videos");

            migrationBuilder.DropTable(
                name: "Aktivitaeten");

            migrationBuilder.DropTable(
                name: "Freundschaften");

            migrationBuilder.DropTable(
                name: "KonzertBands");

            migrationBuilder.DropTable(
                name: "Konzerte");

            migrationBuilder.DropIndex(
                name: "IX_Videos_KonzertId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "KonzertId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "BildUrl",
                table: "Bands");
        }
    }
}
