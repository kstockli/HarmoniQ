using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class VideoErstelltAmUndPersonAnspruch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ErstelltAm",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateTable(
                name: "PersonAnsprueche",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BenutzerId = table.Column<string>(type: "TEXT", nullable: false),
                    Begruendung = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntschiedenAm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonAnsprueche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonAnsprueche_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonAnsprueche_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonAnsprueche_BenutzerId",
                table: "PersonAnsprueche",
                column: "BenutzerId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonAnsprueche_PersonId_BenutzerId_Status",
                table: "PersonAnsprueche",
                columns: new[] { "PersonId", "BenutzerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonAnsprueche_Status",
                table: "PersonAnsprueche",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonAnsprueche");

            migrationBuilder.DropColumn(
                name: "ErstelltAm",
                table: "Videos");
        }
    }
}
