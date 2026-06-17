using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class BestehendeKontenBestaetigt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Einmaliger Aussperr-Schutz: alle bereits existierenden Konten als E-Mail-bestätigt
            // markieren, bevor RequireConfirmedAccount=true greift. Neue Konten sind davon nicht
            // betroffen (Migration läuft genau einmal).
            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"EmailConfirmed\" = 1 WHERE \"EmailConfirmed\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
