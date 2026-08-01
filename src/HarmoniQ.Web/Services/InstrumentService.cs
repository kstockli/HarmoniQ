using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>Find-or-create + Merge für Instrumente – matcht <b>Name ODER Alias</b> (case-insensitiv),
/// damit Import-/Crawler-Varianten („Es-Klarinette", „Klarinette in B", „Bb Clarinet") auf einen
/// kanonischen Eintrag zeigen statt Dubletten anzulegen. Analog Band/Stück/Person.</summary>
public static class InstrumentService
{
    /// <summary>Liefert das Instrument zu <paramref name="name"/> (Name oder Alias); legt es an, wenn
    /// keins existiert. Berücksichtigt auch bereits im Kontext getrackte (Local) Instrumente.</summary>
    public static async Task<Instrument> FindeOderErstelleAsync(ApplicationDbContext db, string name)
    {
        var trimmed = name.Trim();
        var lower = trimmed.ToLower();

        var instrument = await db.Instrumente
            .FirstOrDefaultAsync(i => i.Name.ToLower() == lower || i.Aliase.Any(a => a.Name.ToLower() == lower));
        instrument ??= db.Instrumente.Local.FirstOrDefault(i =>
            string.Equals(i.Name, trimmed, StringComparison.OrdinalIgnoreCase)
            || i.Aliase.Any(a => string.Equals(a.Name, trimmed, StringComparison.OrdinalIgnoreCase)));
        if (instrument == null)
        {
            instrument = new Instrument { Name = trimmed };
            db.Instrumente.Add(instrument);
        }
        return instrument;
    }

    /// <summary>Führt <paramref name="quelleId"/> in <paramref name="zielId"/> zusammen: verschiebt
    /// PersonInstrument- und Stimmen-Referenzen, übernimmt Quell-Name (+ dessen Aliase) als Alias des
    /// Ziels und löscht die Quelle. Speichert selbst.</summary>
    public static async Task MergeAsync(ApplicationDbContext db, Guid quelleId, Guid zielId)
    {
        if (quelleId == zielId) return;
        var quelle = await db.Instrumente.Include(i => i.Aliase).FirstOrDefaultAsync(i => i.Id == quelleId);
        var ziel = await db.Instrumente.Include(i => i.Aliase).FirstOrDefaultAsync(i => i.Id == zielId);
        if (quelle == null || ziel == null) return;

        // PersonInstrumente umhängen (PK = PersonId+InstrumentId → Dubletten vermeiden).
        var zielSet = (await db.PersonInstrumente.Where(pi => pi.InstrumentId == zielId)
            .Select(pi => pi.PersonId).ToListAsync()).ToHashSet();
        foreach (var pi in await db.PersonInstrumente.Where(pi => pi.InstrumentId == quelleId).ToListAsync())
        {
            if (zielSet.Add(pi.PersonId))
                db.PersonInstrumente.Add(new PersonInstrument { PersonId = pi.PersonId, InstrumentId = zielId });
            db.PersonInstrumente.Remove(pi);
        }

        // Stimmen umhängen (Unique = InstrumentId+Bezeichnung).
        var zielStimmen = (await db.Stimmen.Where(s => s.InstrumentId == zielId)
            .Select(s => s.Bezeichnung).ToListAsync()).ToHashSet();
        foreach (var s in await db.Stimmen.Where(s => s.InstrumentId == quelleId).ToListAsync())
        {
            if (zielStimmen.Add(s.Bezeichnung)) s.InstrumentId = zielId;
            else db.Stimmen.Remove(s);
        }

        // Quell-Name + Quell-Aliase als Aliase des Ziels übernehmen (keine Dubletten).
        var vorhanden = ziel.Aliase.Select(a => a.Name).Append(ziel.Name)
            .Select(n => n.ToLowerInvariant()).ToHashSet();
        void AliasHinzu(string n)
        {
            if (!string.IsNullOrWhiteSpace(n) && vorhanden.Add(n.ToLowerInvariant()))
                db.InstrumentAliase.Add(new InstrumentAlias { InstrumentId = zielId, Name = n.Trim() });
        }
        AliasHinzu(quelle.Name);
        foreach (var a in quelle.Aliase) AliasHinzu(a.Name);

        db.Instrumente.Remove(quelle);   // Aliase der Quelle gehen per Cascade weg
        await db.SaveChangesAsync();
    }
}
