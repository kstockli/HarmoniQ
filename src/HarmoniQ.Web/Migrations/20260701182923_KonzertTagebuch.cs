using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class KonzertTagebuch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KonzertBesuche",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KonzertId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenutzerId = table.Column<string>(type: "text", nullable: false),
                    Notiz = table.Column<string>(type: "text", nullable: true),
                    Sichtbarkeit = table.Column<int>(type: "integer", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KonzertBesuche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KonzertBesuche_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KonzertBesuche_Konzerte_KonzertId",
                        column: x => x.KonzertId,
                        principalTable: "Konzerte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StueckEindruecke",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KonzertStueckId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenutzerId = table.Column<string>(type: "text", nullable: false),
                    Sterne = table.Column<int>(type: "integer", nullable: true),
                    Notiz = table.Column<string>(type: "text", nullable: true),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeaendertAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StueckEindruecke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StueckEindruecke_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StueckEindruecke_KonzertStuecke_KonzertStueckId",
                        column: x => x.KonzertStueckId,
                        principalTable: "KonzertStuecke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KonzertBesuche_BenutzerId_KonzertId",
                table: "KonzertBesuche",
                columns: new[] { "BenutzerId", "KonzertId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KonzertBesuche_KonzertId",
                table: "KonzertBesuche",
                column: "KonzertId");

            migrationBuilder.CreateIndex(
                name: "IX_StueckEindruecke_BenutzerId_KonzertStueckId",
                table: "StueckEindruecke",
                columns: new[] { "BenutzerId", "KonzertStueckId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StueckEindruecke_KonzertStueckId",
                table: "StueckEindruecke",
                column: "KonzertStueckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KonzertBesuche");

            migrationBuilder.DropTable(
                name: "StueckEindruecke");
        }
    }
}
