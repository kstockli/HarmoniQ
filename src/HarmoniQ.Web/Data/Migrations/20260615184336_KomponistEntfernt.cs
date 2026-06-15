using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class KomponistEntfernt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stuecke_Komponisten_KomponistId",
                table: "Stuecke");

            migrationBuilder.DropTable(
                name: "Komponisten");

            migrationBuilder.DropIndex(
                name: "IX_Stuecke_KomponistId",
                table: "Stuecke");

            migrationBuilder.DropColumn(
                name: "KomponistId",
                table: "Stuecke");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KomponistId",
                table: "Stuecke",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Komponisten",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BildUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Biografie = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Webseite = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Komponisten", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stuecke_KomponistId",
                table: "Stuecke",
                column: "KomponistId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stuecke_Komponisten_KomponistId",
                table: "Stuecke",
                column: "KomponistId",
                principalTable: "Komponisten",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
