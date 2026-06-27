using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Führt eine Quell-Person in eine Ziel-Person zusammen („hineinschmelzen"). Alle Verweise
/// (Stück-Beiträge, Mitwirkungen, Instrumente, Rollen, Links, Bandmitgliedschaften, Konzert-
/// Mitwirkende, Anträge, Freundschaften, Feed) werden auf die Ziel-Person umgehängt – ohne
/// Dubletten. Der Quell-Name (und ihre Aliase) bleiben als Alias der Ziel-Person erhalten.
/// Danach wird die Quell-Person gelöscht.
/// </summary>
public static class PersonMergeService
{
    public static async Task<(bool Ok, string Meldung)> MergeAsync(ApplicationDbContext db, Guid quelleId, Guid zielId)
    {
        if (quelleId == zielId) return (false, "Quelle und Ziel sind identisch.");

        var quelle = await db.Personen.Include(p => p.Aliase).Include(p => p.Rollen)
            .Include(p => p.Links).Include(p => p.Instrumente).FirstOrDefaultAsync(p => p.Id == quelleId);
        var ziel = await db.Personen.Include(p => p.Aliase).Include(p => p.Rollen)
            .Include(p => p.Links).Include(p => p.Instrumente).FirstOrDefaultAsync(p => p.Id == zielId);
        if (quelle == null || ziel == null) return (false, "Person nicht gefunden.");

        if (quelle.BenutzerId != null && ziel.BenutzerId != null && quelle.BenutzerId != ziel.BenutzerId)
            return (false, "Beide Personen sind mit einem Konto verknüpft – bitte zuerst eine Verknüpfung lösen.");

        // ── StueckBeitrag: umhängen, Dublette (Stück+Rolle) verwerfen ─────────
        var zielBeitraege = (await db.StueckBeitraege.Where(b => b.PersonId == zielId)
            .Select(b => new { b.StueckId, b.Rolle }).ToListAsync())
            .Select(x => (x.StueckId, x.Rolle)).ToHashSet();
        foreach (var b in await db.StueckBeitraege.Where(b => b.PersonId == quelleId).ToListAsync())
        {
            if (zielBeitraege.Contains((b.StueckId, b.Rolle))) db.StueckBeitraege.Remove(b);
            else { b.PersonId = zielId; zielBeitraege.Add((b.StueckId, b.Rolle)); }
        }

        // ── VideoMitwirkung: umhängen, Dublette (Video+Rolle+Instrument) verwerfen ─
        var zielMitw = (await db.VideoMitwirkungen.Where(m => m.PersonId == zielId)
            .Select(m => new { m.VideoId, m.Rolle, m.InstrumentId }).ToListAsync())
            .Select(x => (x.VideoId, x.Rolle, x.InstrumentId)).ToHashSet();
        foreach (var m in await db.VideoMitwirkungen.Where(m => m.PersonId == quelleId).ToListAsync())
        {
            if (zielMitw.Contains((m.VideoId, m.Rolle, m.InstrumentId))) db.VideoMitwirkungen.Remove(m);
            else { m.PersonId = zielId; zielMitw.Add((m.VideoId, m.Rolle, m.InstrumentId)); }
        }

        // ── PersonInstrument (PK Person+Instrument): fehlende ergänzen, Quelle löschen ─
        var zielInstr = await db.PersonInstrumente.Where(pi => pi.PersonId == zielId)
            .Select(pi => pi.InstrumentId).ToHashSetAsync();
        foreach (var pi in await db.PersonInstrumente.Where(pi => pi.PersonId == quelleId).ToListAsync())
        {
            if (!zielInstr.Contains(pi.InstrumentId))
            {
                db.PersonInstrumente.Add(new PersonInstrument { PersonId = zielId, InstrumentId = pi.InstrumentId });
                zielInstr.Add(pi.InstrumentId);
            }
            db.PersonInstrumente.Remove(pi);
        }

        // ── PersonRolle (PK Person+Rolle): fehlende ergänzen, Quelle löschen ──
        var zielRollen = ziel.Rollen.Select(r => r.Rolle).ToHashSet();
        foreach (var r in await db.PersonRollen.Where(r => r.PersonId == quelleId).ToListAsync())
        {
            if (!zielRollen.Contains(r.Rolle))
            {
                db.PersonRollen.Add(new PersonRolle { PersonId = zielId, Rolle = r.Rolle });
                zielRollen.Add(r.Rolle);
            }
            db.PersonRollen.Remove(r);
        }

        // ── PersonLink: fehlende Typen übernehmen (Quelle wird per Cascade gelöscht) ─
        foreach (var l in quelle.Links)
            if (ziel.Links.All(x => x.Typ != l.Typ))
                db.PersonLinks.Add(new PersonLink { PersonId = zielId, Typ = l.Typ, Url = l.Url });

        // ── BandMitgliedschaft: umhängen, Dublette (Band) verwerfen ───────────
        var zielBands = await db.BandMitgliedschaften.Where(m => m.PersonId == zielId)
            .Select(m => m.BandId).ToHashSetAsync();
        foreach (var m in await db.BandMitgliedschaften.Where(m => m.PersonId == quelleId).ToListAsync())
        {
            if (zielBands.Contains(m.BandId)) db.BandMitgliedschaften.Remove(m);
            else { m.PersonId = zielId; zielBands.Add(m.BandId); }
        }

        // ── KonzertPerson: umhängen, Dublette (Konzert+Rolle) verwerfen ───────
        var zielKp = (await db.KonzertPersonen.Where(kp => kp.PersonId == zielId)
            .Select(kp => new { kp.KonzertId, kp.Rolle }).ToListAsync())
            .Select(x => (x.KonzertId, x.Rolle)).ToHashSet();
        foreach (var kp in await db.KonzertPersonen.Where(kp => kp.PersonId == quelleId).ToListAsync())
        {
            if (zielKp.Contains((kp.KonzertId, kp.Rolle))) db.KonzertPersonen.Remove(kp);
            else { kp.PersonId = zielId; zielKp.Add((kp.KonzertId, kp.Rolle)); }
        }

        // ── Anträge: einfach umhängen ─────────────────────────────────────────
        foreach (var a in await db.PersonAnsprueche.Where(a => a.PersonId == quelleId).ToListAsync())
            a.PersonId = zielId;
        foreach (var a in await db.BandbeitrittAntraege.Where(a => a.PersonId == quelleId).ToListAsync())
            a.PersonId = zielId;

        // ── Freundschaften: umhängen, Selbst-/Doppel-Verbindung auflösen ──────
        var zielFreunde = (await db.Freundschaften
                .Where(f => f.AnfragerPersonId == zielId || f.EmpfaengerPersonId == zielId)
                .Select(f => new { f.AnfragerPersonId, f.EmpfaengerPersonId }).ToListAsync())
            .Select(f => f.AnfragerPersonId == zielId ? f.EmpfaengerPersonId : f.AnfragerPersonId)
            .ToHashSet();
        foreach (var f in await db.Freundschaften
            .Where(f => f.AnfragerPersonId == quelleId || f.EmpfaengerPersonId == quelleId).ToListAsync())
        {
            var andere = f.AnfragerPersonId == quelleId ? f.EmpfaengerPersonId : f.AnfragerPersonId;
            if (andere == zielId || zielFreunde.Contains(andere)) { db.Freundschaften.Remove(f); continue; }
            if (f.AnfragerPersonId == quelleId) f.AnfragerPersonId = zielId; else f.EmpfaengerPersonId = zielId;
            zielFreunde.Add(andere);
        }

        // ── Feed: Akteur/Neben umhängen ───────────────────────────────────────
        foreach (var akt in await db.Aktivitaeten.Where(a => a.AkteurPersonId == quelleId).ToListAsync())
            akt.AkteurPersonId = zielId;
        foreach (var akt in await db.Aktivitaeten.Where(a => a.NebenPersonId == quelleId).ToListAsync())
            akt.NebenPersonId = akt.AkteurPersonId == zielId ? null : zielId;

        // ── Konto-Verknüpfung übernehmen (falls nur Quelle verknüpft) ─────────
        if (ziel.BenutzerId == null && quelle.BenutzerId != null)
        {
            var konto = quelle.BenutzerId;
            quelle.BenutzerId = null;   // erst lösen (UNIQUE), dann am Ziel setzen
            ziel.BenutzerId = konto;
        }

        // ── Stammdaten-Lücken des Ziels aus der Quelle füllen ─────────────────
        if (string.IsNullOrWhiteSpace(ziel.Biografie)) ziel.Biografie = quelle.Biografie;
        ziel.BildUrl ??= quelle.BildUrl;
        ziel.Geburtsjahr ??= quelle.Geburtsjahr;

        // ── Aliase: Quell-Name + Quell-Aliase als Aliase des Ziels sichern ────
        var bekannt = new HashSet<string>(
            ziel.Aliase.Select(a => a.Name).Append(ziel.Name), StringComparer.OrdinalIgnoreCase);
        void AliasSichern(string name)
        {
            name = name.Trim();
            if (name.Length > 0 && bekannt.Add(name))
                db.PersonAliase.Add(new PersonAlias { PersonId = zielId, Name = name });
        }
        AliasSichern(quelle.Name);
        foreach (var a in quelle.Aliase) AliasSichern(a.Name);

        db.Personen.Remove(quelle);
        await db.SaveChangesAsync();
        return (true, $"„{quelle.Name}“ wurde in „{ziel.Name}“ zusammengeführt.");
    }
}
