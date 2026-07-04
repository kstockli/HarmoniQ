using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class AuditSpalten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Videos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Videos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "VideoMitwirkungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "VideoMitwirkungen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "VideoMitwirkungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "VideoMitwirkungen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "StueckEindruecke",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "StueckEindruecke",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "StueckEindruecke",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "StueckEindruecke",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Stuecke",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Stuecke",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Stuecke",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Stuecke",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "StueckBeitraege",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "StueckBeitraege",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "StueckBeitraege",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "StueckBeitraege",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "StueckAliase",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "StueckAliase",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "StueckAliase",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "StueckAliase",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Stimmen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Stimmen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Stimmen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Stimmen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Richtigstellungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Richtigstellungen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Richtigstellungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Richtigstellungen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "PersonRollen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "PersonRollen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "PersonRollen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "PersonRollen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "PersonLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "PersonLinks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "PersonLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "PersonLinks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "PersonInstrumente",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "PersonInstrumente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "PersonInstrumente",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "PersonInstrumente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Personen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Personen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Personen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Personen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "PersonAnsprueche",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "PersonAnsprueche",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "PersonAnsprueche",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "PersonAnsprueche",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "PersonAliase",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "PersonAliase",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "PersonAliase",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "PersonAliase",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Lokale",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Lokale",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Lokale",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Lokale",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "LokalAliase",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "LokalAliase",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "LokalAliase",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "LokalAliase",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "KonzertStuecke",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "KonzertStuecke",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "KonzertStuecke",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "KonzertStuecke",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "KonzertPersonen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "KonzertPersonen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "KonzertPersonen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "KonzertPersonen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Konzerte",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Konzerte",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Konzerte",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Konzerte",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "KonzertBesuche",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "KonzertBesuche",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "KonzertBesuche",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "KonzertBesuche",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "KonzertBands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "KonzertBands",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "KonzertBands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "KonzertBands",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Instrumente",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Instrumente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Instrumente",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Instrumente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Freundschaften",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Freundschaften",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Freundschaften",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Freundschaften",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "CrawlSeiten",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "CrawlSeiten",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "CrawlSeiten",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "CrawlSeiten",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "CrawlQuellen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "CrawlQuellen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "CrawlQuellen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "CrawlQuellen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "CrawlLaeufe",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "CrawlLaeufe",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "CrawlLaeufe",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "CrawlLaeufe",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "CrawlFunde",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "CrawlFunde",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "CrawlFunde",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "CrawlFunde",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Bewertungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Bewertungen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Bewertungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Bewertungen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Bands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Bands",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Bands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Bands",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "BandMitgliedschaften",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "BandMitgliedschaften",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "BandMitgliedschaften",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "BandMitgliedschaften",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "BandLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "BandLinks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "BandLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "BandLinks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "BandbeitrittAntraege",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "BandbeitrittAntraege",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "BandbeitrittAntraege",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "BandbeitrittAntraege",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "BandAliase",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "BandAliase",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "BandAliase",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "BandAliase",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createtime",
                table: "Aktivitaeten",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createuser",
                table: "Aktivitaeten",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modifytime",
                table: "Aktivitaeten",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifyuser",
                table: "Aktivitaeten",
                type: "text",
                nullable: true);

            // Bestands-Baseline: alle bestehenden Zeilen jeder Tabelle mit createtime-Spalte einmalig
            // auf 2026-06-30 / me@q-no.ch setzen (vor Einführung der Audit-Spalten).
            migrationBuilder.Sql(@"
                DO $$
                DECLARE t text;
                BEGIN
                    FOR t IN
                        SELECT table_name FROM information_schema.columns
                        WHERE table_schema = 'public' AND column_name = 'createtime'
                    LOOP
                        EXECUTE format(
                            'UPDATE %I SET createtime = TIMESTAMPTZ ''2026-06-30 00:00:00+00'', ' ||
                            'modifytime = TIMESTAMPTZ ''2026-06-30 00:00:00+00'', ' ||
                            'createuser = ''me@q-no.ch'', modifyuser = ''me@q-no.ch'' ' ||
                            'WHERE createtime IS NULL', t);
                    END LOOP;
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "VideoMitwirkungen");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "VideoMitwirkungen");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "VideoMitwirkungen");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "VideoMitwirkungen");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "StueckEindruecke");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "StueckEindruecke");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "StueckEindruecke");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "StueckEindruecke");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Stuecke");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Stuecke");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Stuecke");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Stuecke");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "StueckBeitraege");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "StueckBeitraege");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "StueckBeitraege");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "StueckBeitraege");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "StueckAliase");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "StueckAliase");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "StueckAliase");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "StueckAliase");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Stimmen");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Stimmen");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Stimmen");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Stimmen");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Richtigstellungen");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Richtigstellungen");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Richtigstellungen");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Richtigstellungen");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "PersonRollen");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "PersonRollen");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "PersonRollen");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "PersonRollen");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "PersonLinks");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "PersonLinks");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "PersonLinks");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "PersonLinks");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "PersonInstrumente");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "PersonInstrumente");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "PersonInstrumente");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "PersonInstrumente");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "PersonAnsprueche");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "PersonAnsprueche");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "PersonAnsprueche");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "PersonAnsprueche");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "PersonAliase");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "PersonAliase");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "PersonAliase");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "PersonAliase");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Lokale");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Lokale");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Lokale");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Lokale");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "LokalAliase");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "LokalAliase");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "LokalAliase");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "LokalAliase");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "KonzertStuecke");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "KonzertStuecke");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "KonzertStuecke");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "KonzertStuecke");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "KonzertPersonen");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "KonzertPersonen");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "KonzertPersonen");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "KonzertPersonen");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Konzerte");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Konzerte");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Konzerte");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Konzerte");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "KonzertBesuche");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "KonzertBesuche");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "KonzertBesuche");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "KonzertBesuche");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "KonzertBands");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "KonzertBands");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "KonzertBands");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "KonzertBands");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Instrumente");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Instrumente");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Instrumente");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Instrumente");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Freundschaften");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Freundschaften");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Freundschaften");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Freundschaften");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "CrawlSeiten");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "CrawlSeiten");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "CrawlSeiten");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "CrawlSeiten");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "CrawlQuellen");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "CrawlQuellen");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "CrawlQuellen");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "CrawlQuellen");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "CrawlLaeufe");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "CrawlLaeufe");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "CrawlLaeufe");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "CrawlLaeufe");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "CrawlFunde");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "CrawlFunde");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "CrawlFunde");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "CrawlFunde");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Bewertungen");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Bewertungen");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Bewertungen");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Bewertungen");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Bands");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Bands");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Bands");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Bands");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "BandMitgliedschaften");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "BandMitgliedschaften");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "BandMitgliedschaften");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "BandMitgliedschaften");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "BandLinks");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "BandLinks");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "BandLinks");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "BandLinks");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "BandbeitrittAntraege");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "BandbeitrittAntraege");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "BandbeitrittAntraege");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "BandbeitrittAntraege");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "BandAliase");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "BandAliase");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "BandAliase");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "BandAliase");

            migrationBuilder.DropColumn(
                name: "createtime",
                table: "Aktivitaeten");

            migrationBuilder.DropColumn(
                name: "createuser",
                table: "Aktivitaeten");

            migrationBuilder.DropColumn(
                name: "modifytime",
                table: "Aktivitaeten");

            migrationBuilder.DropColumn(
                name: "modifyuser",
                table: "Aktivitaeten");
        }
    }
}
