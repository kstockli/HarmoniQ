using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Führt eine Quell-Band in eine Ziel-Band zusammen („hineinschmelzen"). Alle Verweise (Videos,
/// Mitgliedschaften, Konzert-Teilnahmen, Programm, Mitwirkende, Anträge, Links) werden auf die
/// Ziel-Band umgehängt – ohne Dubletten. Der Quell-Name (und ihre Aliase) bleiben als Alias der
/// Ziel-Band erhalten. Danach wird die Quell-Band gelöscht.
/// </summary>
public static class BandMergeService
{
    public static async Task<(bool Ok, string Meldung)> MergeAsync(ApplicationDbContext db, Guid quelleId, Guid zielId)
    {
        if (quelleId == zielId) return (false, "Quelle und Ziel sind identisch.");

        var quelle = await db.Bands.Include(b => b.Aliase).Include(b => b.Links).FirstOrDefaultAsync(b => b.Id == quelleId);
        var ziel = await db.Bands.Include(b => b.Aliase).Include(b => b.Links).FirstOrDefaultAsync(b => b.Id == zielId);
        if (quelle == null || ziel == null) return (false, "Band nicht gefunden.");

        // ── Videos: einfach umhängen ──────────────────────────────────────────
        foreach (var v in await db.Videos.Where(v => v.BandId == quelleId).ToListAsync())
            v.BandId = zielId;

        // ── Mitgliedschaften: umhängen, Dublette (gleiche Person) verwerfen ────
        var zielMitglieder = await db.BandMitgliedschaften.Where(m => m.BandId == zielId)
            .Select(m => m.PersonId).ToHashSetAsync();
        foreach (var m in await db.BandMitgliedschaften.Where(m => m.BandId == quelleId).ToListAsync())
        {
            if (zielMitglieder.Contains(m.PersonId)) db.BandMitgliedschaften.Remove(m);
            else { m.BandId = zielId; zielMitglieder.Add(m.PersonId); }
        }

        // ── KonzertBand (PK Konzert+Band): fehlende ergänzen, Quelle entfernen ─
        var zielKonzerte = await db.KonzertBands.Where(kb => kb.BandId == zielId)
            .Select(kb => kb.KonzertId).ToHashSetAsync();
        foreach (var kb in await db.KonzertBands.Where(kb => kb.BandId == quelleId).ToListAsync())
        {
            if (!zielKonzerte.Contains(kb.KonzertId))
            {
                db.KonzertBands.Add(new KonzertBand { KonzertId = kb.KonzertId, BandId = zielId });
                zielKonzerte.Add(kb.KonzertId);
            }
            db.KonzertBands.Remove(kb);
        }

        // ── KonzertStueck: umhängen, Dublette (Konzert+Stück) verwerfen ────────
        var zielProgramm = await db.KonzertStuecke.Where(ks => ks.BandId == zielId)
            .Select(ks => new { ks.KonzertId, ks.StueckId }).ToListAsync();
        var zielSet = zielProgramm.Select(x => (x.KonzertId, x.StueckId)).ToHashSet();
        foreach (var ks in await db.KonzertStuecke.Where(ks => ks.BandId == quelleId).ToListAsync())
        {
            if (zielSet.Contains((ks.KonzertId, ks.StueckId))) db.KonzertStuecke.Remove(ks);
            else { ks.BandId = zielId; zielSet.Add((ks.KonzertId, ks.StueckId)); }
        }

        // ── KonzertPerson & Anträge: einfach umhängen ─────────────────────────
        foreach (var kp in await db.KonzertPersonen.Where(kp => kp.BandId == quelleId).ToListAsync())
            kp.BandId = zielId;
        foreach (var a in await db.BandbeitrittAntraege.Where(a => a.BandId == quelleId).ToListAsync())
            a.BandId = zielId;

        // ── Stammdaten-Lücken der Ziel-Band aus der Quelle füllen ─────────────
        ziel.Land ??= quelle.Land;
        ziel.Webseite ??= quelle.Webseite;
        ziel.BildUrl ??= quelle.BildUrl;
        ziel.Kategorie ??= quelle.Kategorie;
        ziel.Staerkeklasse ??= quelle.Staerkeklasse;
        ziel.Gruendungsjahr ??= quelle.Gruendungsjahr;
        ziel.Geschichte ??= quelle.Geschichte;

        // ── Links: fehlende Typen übernehmen ──────────────────────────────────
        foreach (var l in quelle.Links)
            if (ziel.Links.All(x => x.Typ != l.Typ))
                db.BandLinks.Add(new BandLink { BandId = zielId, Typ = l.Typ, Url = l.Url });

        // ── Aliase: Quell-Name + Quell-Aliase als Aliase des Ziels sichern ────
        var bekannt = new HashSet<string>(
            ziel.Aliase.Select(a => a.Name).Append(ziel.Name), StringComparer.OrdinalIgnoreCase);
        void AliasSichern(string name)
        {
            if (!bekannt.Contains(name)) { db.BandAliase.Add(new BandAlias { BandId = zielId, Name = name }); bekannt.Add(name); }
        }
        AliasSichern(quelle.Name);
        foreach (var a in quelle.Aliase) AliasSichern(a.Name);

        // Quell-Aliase/-Links werden mit der Band per Cascade gelöscht.
        db.Bands.Remove(quelle);

        await db.SaveChangesAsync();
        return (true, $"„{quelle.Name}“ wurde in „{ziel.Name}“ zusammengeführt.");
    }
}
