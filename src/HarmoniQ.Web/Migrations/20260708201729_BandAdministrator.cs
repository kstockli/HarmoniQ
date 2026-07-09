using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BandAdministrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BandAdministratoren",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BenutzerId = table.Column<string>(type: "text", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: false),
                    createtime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createuser = table.Column<string>(type: "text", nullable: true),
                    modifytime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modifyuser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandAdministratoren", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandAdministratoren_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BandAdministratoren_BandId",
                table: "BandAdministratoren",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_BandAdministratoren_BenutzerId_BandId",
                table: "BandAdministratoren",
                columns: new[] { "BenutzerId", "BandId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BandAdministratoren");
        }
    }
}
