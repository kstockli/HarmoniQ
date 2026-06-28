using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Legt ein ganzes Konzert in einem Schritt an oder bearbeitet es (Wizard
/// <c>/admin/konzerte/erfassen</c> bzw. <c>/admin/konzerte/{id}/bearbeiten</c>):
/// Konzert-Kopf, Programm (Stück + optional Komponist:in + optional Band) und Mitwirkende
/// (Person + Rolle + optional Band). Fehlende Stammdaten (Stück, Komponist:in, Band, Person)
/// werden per Find-or-create angelegt – ohne Dubletten (Abgleich über Name/Titel, normalisiert).
/// </summary>
public static class KonzertErfassungService
{
    public record ProgrammEingabe(string StueckTitel, string? KomponistName, string? BandName, int? Reihenfolge,
        string? ArrangeurName = null);
    public record MitwirkendeEingabe(string PersonName, PersonRolleTyp Rolle, string? BandName);

    public record Eingabe(
        DateOnly Datum,
        string? Name,
        string? Ort,
        string? Beschreibung,
        string? BildUrl,
        IReadOnlyList<ProgrammEingabe> Programm,
        IReadOnlyList<MitwirkendeEingabe> Mitwirkende);

    /// <summary>Speichert ein neues Konzert und gibt dessen Id zurück.</summary>
    public static async Task<Guid> ErfasseAsync(ApplicationDbContext db, Eingabe e)
    {
        var konzert = new Konzert();
        db.Konzerte.Add(konzert);
        KopfSetzen(konzert, e);

        var desiredBands = new HashSet<Guid>();
        await BefuelleAsync(db, konzert, e, desiredBands);
        foreach (var bid in desiredBands)
            db.KonzertBands.Add(new KonzertBand { Konzert = konzert, BandId = bid });

        await db.SaveChangesAsync();
        return konzert.Id;
    }

    /// <summary>Find-or-create für Konzerte (verhindert Dubletten bei wiederholtem Crawler-Import).
    /// Identität = gleiches <b>Datum + Name + Ort</b>; gibt es zusätzlich Band-Angaben in der Eingabe, muss
    /// auch mindestens <b>eine Band übereinstimmen</b> (sonst sind es verschiedene Konzerte – z. B. mehrere
    /// „Jahreskonzerte" am selben Samstag im selben Saal). Ohne Namen wird immer neu angelegt. Gibt die Id zurück.</summary>
    public static async Task<Guid> ErfasseOderAktualisiereAsync(ApplicationDbContext db, Eingabe e)
    {
        var name = Leer(e.Name);
        if (name != null)
        {
            var ort = Leer(e.Ort);
            var kandidaten = await db.Konzerte
                .Where(k => k.Datum == e.Datum && k.Name == name && (ort == null || k.Ort == ort))
                .Select(k => k.Id)
                .ToListAsync();

            if (kandidaten.Count > 0)
            {
                // Bandnamen der Eingabe (aus Programm + Mitwirkenden), normalisiert.
                var bands = e.Programm.Select(p => p.BandName)
                    .Concat(e.Mitwirkende.Select(m => m.BandName))
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .Select(b => b!.Trim().ToLowerInvariant())
                    .ToHashSet();

                Guid? treffer = null;
                if (bands.Count == 0)
                {
                    treffer = kandidaten[0]; // keine Band-Info → über (Datum, Name, Ort) identifizieren
                }
                else
                {
                    foreach (var kid in kandidaten)
                    {
                        var vorhandeneBands = await db.KonzertBands.Where(kb => kb.KonzertId == kid)
                            .Select(kb => kb.Band.Name).ToListAsync();
                        if (vorhandeneBands.Any(n => bands.Contains(n.Trim().ToLowerInvariant()))) { treffer = kid; break; }
                    }
                }

                if (treffer is { } id)
                {
                    await EditAsync(db, id, e);
                    return id;
                }
            }
        }
        return await ErfasseAsync(db, e);
    }

    /// <summary>Bearbeitet ein bestehendes Konzert: Kopf, Programm und Mitwirkende werden ersetzt.</summary>
    public static async Task EditAsync(ApplicationDbContext db, Guid konzertId, Eingabe e)
    {
        var konzert = await db.Konzerte.FirstOrDefaultAsync(k => k.Id == konzertId)
            ?? throw new InvalidOperationException("Konzert nicht gefunden.");
        KopfSetzen(konzert, e);

        // Programm und Mitwirkende komplett neu aufbauen (Surrogat-PKs → unkritisch).
        db.KonzertStuecke.RemoveRange(await db.KonzertStuecke.Where(x => x.KonzertId == konzertId).ToListAsync());
        db.KonzertPersonen.RemoveRange(await db.KonzertPersonen.Where(x => x.KonzertId == konzertId).ToListAsync());

        var desiredBands = new HashSet<Guid>();
        await BefuelleAsync(db, konzert, e, desiredBands);

        // Bands, die ein Video an diesem Konzert haben, bleiben Teilnehmerinnen.
        var videoBands = await db.Videos
            .Where(v => v.KonzertId == konzertId && v.BandId != null)
            .Select(v => v.BandId!.Value).Distinct().ToListAsync();
        foreach (var bid in videoBands) desiredBands.Add(bid);

        // KonzertBand differenziell abgleichen (PK = Konzert+Band → kein Remove/Add derselben Zeile).
        var bestehend = await db.KonzertBands.Where(kb => kb.KonzertId == konzertId).ToListAsync();
        foreach (var weg in bestehend.Where(kb => !desiredBands.Contains(kb.BandId)))
            db.KonzertBands.Remove(weg);
        foreach (var neu in desiredBands.Where(id => bestehend.All(kb => kb.BandId != id)))
            db.KonzertBands.Add(new KonzertBand { KonzertId = konzertId, BandId = neu });

        await db.SaveChangesAsync();
    }

    private static void KopfSetzen(Konzert k, Eingabe e)
    {
        k.Datum = e.Datum;
        k.Name = Leer(e.Name);
        k.Ort = Leer(e.Ort);
        k.Beschreibung = Leer(e.Beschreibung);
        k.BildUrl = Leer(e.BildUrl);
    }

    /// <summary>Baut Programm + Mitwirkende auf (Find-or-create) und sammelt die beteiligten
    /// Band-Ids in <paramref name="desiredBands"/>. Verwaltet <c>KonzertBand</c> NICHT selbst.</summary>
    private static async Task BefuelleAsync(ApplicationDbContext db, Konzert konzert, Eingabe e,
        HashSet<Guid> desiredBands)
    {
        var bandCache = new Dictionary<string, Band>(StringComparer.OrdinalIgnoreCase);
        var stueckCache = new Dictionary<string, Stueck>(StringComparer.OrdinalIgnoreCase);
        var personCache = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        var konzertStuecke = new HashSet<(Guid Stueck, Guid? Band)>();
        var konzertPersonen = new HashSet<(Guid Person, PersonRolleTyp Rolle)>();

        async Task<Band?> BandHolen(string? name)
        {
            name = name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (bandCache.TryGetValue(name, out var b)) return b;
            b = await db.Bands.FirstOrDefaultAsync(x => x.Name == name)
                ?? await db.Bands.FirstOrDefaultAsync(x => x.Aliase.Any(a => a.Name == name))
                ?? db.Bands.Local.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (b == null)
            {
                b = new Band { Name = name };
                db.Bands.Add(b);
            }
            bandCache[name] = b;
            desiredBands.Add(b.Id);
            return b;
        }

        async Task<Person> PersonHolen(string name, PersonRolleTyp rolle)
        {
            name = name.Trim();
            if (!personCache.TryGetValue(name, out var p))
            {
                p = await db.Personen.Include(x => x.Rollen).FirstOrDefaultAsync(x => x.Name == name)
                    ?? await db.Personen.Include(x => x.Rollen).FirstOrDefaultAsync(x => x.Aliase.Any(a => a.Name == name))
                    ?? db.Personen.Local.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (p == null)
                {
                    var sicht = rolle == PersonRolleTyp.Komponist ? Sichtbarkeit.Oeffentlich : Sichtbarkeit.NurInitialen;
                    p = new Person { Name = name, Sichtbarkeit = sicht };
                    db.Personen.Add(p);
                }
                personCache[name] = p;
            }
            if (p.Rollen.All(r => r.Rolle != rolle))
                p.Rollen.Add(new PersonRolle { Rolle = rolle });
            return p;
        }

        // ── Programm ──────────────────────────────────────────────────────────
        foreach (var row in e.Programm)
        {
            var titel = row.StueckTitel.Trim();
            if (titel.Length == 0) continue;

            if (!stueckCache.TryGetValue(titel, out var stueck))
            {
                stueck = await db.Stuecke.FirstOrDefaultAsync(x => x.Titel == titel || x.Aliase.Any(a => a.Name == titel))
                    ?? db.Stuecke.Local.FirstOrDefault(x => string.Equals(x.Titel, titel, StringComparison.OrdinalIgnoreCase));
                if (stueck == null)
                {
                    stueck = new Stueck { Titel = titel };
                    db.Stuecke.Add(stueck);
                    // Komponist:in-/Arrangeur:in-Feld zerlegen: mehrere Namen trennen + Arr.-Marker
                    // erkennen (→ Rolle Arrangeur). Personen-Rolle bleibt Komponist:in (Sichtbarkeit).
                    foreach (var beitrag in KomponistParser.Parse(row.KomponistName, row.ArrangeurName))
                    {
                        var person = await PersonHolen(beitrag.Name, PersonRolleTyp.Komponist);
                        db.StueckBeitraege.Add(new StueckBeitrag
                        {
                            Stueck = stueck, Person = person, Rolle = beitrag.Rolle
                        });
                    }
                }
                stueckCache[titel] = stueck;
            }

            var band = await BandHolen(row.BandName);
            if (konzertStuecke.Add((stueck.Id, band?.Id)))
                db.KonzertStuecke.Add(new KonzertStueck
                {
                    Konzert = konzert, Stueck = stueck, BandId = band?.Id, Reihenfolge = row.Reihenfolge
                });
        }

        // ── Mitwirkende ───────────────────────────────────────────────────────
        foreach (var row in e.Mitwirkende)
        {
            if (string.IsNullOrWhiteSpace(row.PersonName)) continue;
            var person = await PersonHolen(row.PersonName, row.Rolle);
            var band = await BandHolen(row.BandName);
            if (konzertPersonen.Add((person.Id, row.Rolle)))
                db.KonzertPersonen.Add(new KonzertPerson
                {
                    Konzert = konzert, Person = person, Rolle = row.Rolle, BandId = band?.Id
                });
        }
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
