using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BandAdminEinladung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BandAdminEinladungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: false),
                    EingeladenVon = table.Column<string>(type: "text", nullable: true),
                    AblaufAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    createtime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createuser = table.Column<string>(type: "text", nullable: true),
                    modifytime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modifyuser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandAdminEinladungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandAdminEinladungen_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BandAdminEinladungen_BandId",
                table: "BandAdminEinladungen",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_BandAdminEinladungen_Token",
                table: "BandAdminEinladungen",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BandAdminEinladungen");
        }
    }
}
