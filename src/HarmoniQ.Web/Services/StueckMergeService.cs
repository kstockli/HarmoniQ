using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Führt ein Quell-Stück in ein Ziel-Stück zusammen („hineinschmelzen"). Alle Verweise
/// (Programm-Punkte <see cref="KonzertStueck"/>, Beiträge <see cref="StueckBeitrag"/>, Videos)
/// werden auf das Ziel-Stück umgehängt – ohne Dubletten. Der Quell-Titel (und seine Aliase)
/// bleiben als Alias des Ziel-Stücks erhalten. Danach wird das Quell-Stück gelöscht.
/// </summary>
public static class StueckMergeService
{
    public static async Task<(bool Ok, string Meldung)> MergeAsync(ApplicationDbContext db, Guid quelleId, Guid zielId)
    {
        if (quelleId == zielId) return (false, "Quelle und Ziel sind identisch.");

        var quelle = await db.Stuecke.Include(s => s.Aliase).FirstOrDefaultAsync(s => s.Id == quelleId);
        var ziel = await db.Stuecke.Include(s => s.Aliase).FirstOrDefaultAsync(s => s.Id == zielId);
        if (quelle == null || ziel == null) return (false, "Stück nicht gefunden.");

        // ── Videos: einfach umhängen ──────────────────────────────────────────
        foreach (var v in await db.Videos.Where(v => v.StueckId == quelleId).ToListAsync())
            v.StueckId = zielId;

        // ── KonzertStueck: umhängen, Dublette (Konzert+Band) verwerfen ────────
        var zielProgramm = await db.KonzertStuecke.Where(ks => ks.StueckId == zielId)
            .Select(ks => new { ks.KonzertId, ks.BandId }).ToListAsync();
        var zielSet = zielProgramm.Select(x => (x.KonzertId, x.BandId)).ToHashSet();
        foreach (var ks in await db.KonzertStuecke.Where(ks => ks.StueckId == quelleId).ToListAsync())
        {
            if (zielSet.Contains((ks.KonzertId, ks.BandId))) db.KonzertStuecke.Remove(ks);
            else { ks.StueckId = zielId; zielSet.Add((ks.KonzertId, ks.BandId)); }
        }

        // ── StueckBeitrag: umhängen, Dublette (Person+Rolle) verwerfen ─────────
        var zielBeitraege = await db.StueckBeitraege.Where(b => b.StueckId == zielId)
            .Select(b => new { b.PersonId, b.Rolle }).ToListAsync();
        var zielBSet = zielBeitraege.Select(x => (x.PersonId, x.Rolle)).ToHashSet();
        foreach (var b in await db.StueckBeitraege.Where(b => b.StueckId == quelleId).ToListAsync())
        {
            if (zielBSet.Contains((b.PersonId, b.Rolle))) db.StueckBeitraege.Remove(b);
            else { b.StueckId = zielId; zielBSet.Add((b.PersonId, b.Rolle)); }
        }

        // ── Stammdaten-Lücken des Ziel-Stücks aus der Quelle füllen ───────────
        ziel.Jahr ??= quelle.Jahr;
        if (ziel.Schwierigkeitsgrad == Schwierigkeitsgrad.Unbekannt) ziel.Schwierigkeitsgrad = quelle.Schwierigkeitsgrad;
        if (string.IsNullOrWhiteSpace(ziel.Besetzung)) ziel.Besetzung = quelle.Besetzung;
        if (string.IsNullOrWhiteSpace(ziel.Beschreibung)) ziel.Beschreibung = quelle.Beschreibung;
        if (string.IsNullOrWhiteSpace(ziel.OriginalUrl)) ziel.OriginalUrl = quelle.OriginalUrl;

        // ── Aliase: Quell-Titel + Quell-Aliase als Aliase des Ziels sichern ───
        var bekannt = new HashSet<string>(
            ziel.Aliase.Select(a => a.Name).Append(ziel.Titel), StringComparer.OrdinalIgnoreCase);
        void AliasSichern(string name)
        {
            name = name.Trim();
            if (name.Length > 0 && bekannt.Add(name))
                db.StueckAliase.Add(new StueckAlias { StueckId = zielId, Name = name });
        }
        AliasSichern(quelle.Titel);
        foreach (var a in quelle.Aliase) AliasSichern(a.Name);

        // Quell-Aliase werden mit dem Stück per Cascade gelöscht.
        db.Stuecke.Remove(quelle);

        await db.SaveChangesAsync();
        return (true, $"„{quelle.Titel}“ wurde in „{ziel.Titel}“ zusammengeführt.");
    }
}
