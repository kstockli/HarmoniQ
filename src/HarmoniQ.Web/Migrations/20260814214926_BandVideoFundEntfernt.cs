using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BandVideoFundEntfernt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BandVideoFunde");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BandVideoFunde",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: false),
                    createtime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createuser = table.Column<string>(type: "text", nullable: true),
                    EntschiedenAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErgebnisVideoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternId = table.Column<string>(type: "text", nullable: false),
                    GefundenAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KanalName = table.Column<string>(type: "text", nullable: true),
                    KomponistVorschlag = table.Column<string>(type: "text", nullable: true),
                    modifytime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modifyuser = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StueckVorschlag = table.Column<string>(type: "text", nullable: true),
                    Titel = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandVideoFunde", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandVideoFunde_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BandVideoFunde_BandId_ExternId",
                table: "BandVideoFunde",
                columns: new[] { "BandId", "ExternId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BandVideoFunde_Status",
                table: "BandVideoFunde",
                column: "Status");
        }
    }
}
