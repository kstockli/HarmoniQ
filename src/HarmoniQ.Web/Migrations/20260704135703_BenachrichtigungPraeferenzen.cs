using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BenachrichtigungPraeferenzen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenachrichtigungenGesendet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BenutzerId = table.Column<string>(type: "text", nullable: false),
                    Typ = table.Column<int>(type: "integer", nullable: false),
                    EntitaetId = table.Column<Guid>(type: "uuid", nullable: false),
                    GesendetAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createtime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createuser = table.Column<string>(type: "text", nullable: true),
                    modifytime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modifyuser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenachrichtigungenGesendet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenachrichtigungenGesendet_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BenachrichtigungPraeferenzen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BenutzerId = table.Column<string>(type: "text", nullable: false),
                    EmailAktiv = table.Column<bool>(type: "boolean", nullable: false),
                    PushAktiv = table.Column<bool>(type: "boolean", nullable: false),
                    AbmeldeToken = table.Column<Guid>(type: "uuid", nullable: false),
                    createtime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createuser = table.Column<string>(type: "text", nullable: true),
                    modifytime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modifyuser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenachrichtigungPraeferenzen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenachrichtigungPraeferenzen_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungenGesendet_BenutzerId_Typ_EntitaetId",
                table: "BenachrichtigungenGesendet",
                columns: new[] { "BenutzerId", "Typ", "EntitaetId" });

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungPraeferenzen_AbmeldeToken",
                table: "BenachrichtigungPraeferenzen",
                column: "AbmeldeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungPraeferenzen_BenutzerId",
                table: "BenachrichtigungPraeferenzen",
                column: "BenutzerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenachrichtigungenGesendet");

            migrationBuilder.DropTable(
                name: "BenachrichtigungPraeferenzen");
        }
    }
}
