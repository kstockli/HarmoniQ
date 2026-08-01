using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class InstrumentAliasFamilieSymbol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Familie",
                table: "Instrumente",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SymbolUrl",
                table: "Instrumente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WikipediaUrl",
                table: "Instrumente",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstrumentAliase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    createtime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createuser = table.Column<string>(type: "text", nullable: true),
                    modifytime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modifyuser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentAliase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentAliase_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentAliase_InstrumentId_Name",
                table: "InstrumentAliase",
                columns: new[] { "InstrumentId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstrumentAliase");

            migrationBuilder.DropColumn(
                name: "Familie",
                table: "Instrumente");

            migrationBuilder.DropColumn(
                name: "SymbolUrl",
                table: "Instrumente");

            migrationBuilder.DropColumn(
                name: "WikipediaUrl",
                table: "Instrumente");
        }
    }
}
