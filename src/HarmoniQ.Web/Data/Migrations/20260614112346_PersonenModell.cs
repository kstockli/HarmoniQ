using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class PersonenModell : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Personen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Sichtbarkeit = table.Column<int>(type: "INTEGER", nullable: false),
                    Biografie = table.Column<string>(type: "TEXT", nullable: true),
                    BildUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Geburtsjahr = table.Column<int>(type: "INTEGER", nullable: true),
                    BenutzerId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personen_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PersonLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Typ = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonLinks_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonRollen",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rolle = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonRollen", x => new { x.PersonId, x.Rolle });
                    table.ForeignKey(
                        name: "FK_PersonRollen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StueckBeitraege",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StueckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rolle = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StueckBeitraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StueckBeitraege_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StueckBeitraege_Stuecke_StueckId",
                        column: x => x.StueckId,
                        principalTable: "Stuecke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Personen_BenutzerId",
                table: "Personen",
                column: "BenutzerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonLinks_PersonId",
                table: "PersonLinks",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_StueckBeitraege_PersonId",
                table: "StueckBeitraege",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_StueckBeitraege_StueckId_PersonId_Rolle",
                table: "StueckBeitraege",
                columns: new[] { "StueckId", "PersonId", "Rolle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonLinks");

            migrationBuilder.DropTable(
                name: "PersonRollen");

            migrationBuilder.DropTable(
                name: "StueckBeitraege");

            migrationBuilder.DropTable(
                name: "Personen");
        }
    }
}
