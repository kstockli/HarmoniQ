using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BandHeimatLokal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HeimatLokalId",
                table: "Bands",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bands_HeimatLokalId",
                table: "Bands",
                column: "HeimatLokalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bands_Lokale_HeimatLokalId",
                table: "Bands",
                column: "HeimatLokalId",
                principalTable: "Lokale",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bands_Lokale_HeimatLokalId",
                table: "Bands");

            migrationBuilder.DropIndex(
                name: "IX_Bands_HeimatLokalId",
                table: "Bands");

            migrationBuilder.DropColumn(
                name: "HeimatLokalId",
                table: "Bands");
        }
    }
}
