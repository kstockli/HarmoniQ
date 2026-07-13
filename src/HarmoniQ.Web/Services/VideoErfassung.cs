using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Gemeinsame Find-or-create-Logik für die Video-Erfassung (Einzel-Link <c>BandVideoHinzufuegen</c>
/// und YouTube-Band-Crawler <c>BandVideoCrawlService</c>): findet ein vorhandenes Stück (über Titel
/// oder Alias) bzw. legt es neu an – mit Komponist:in(nen) als Beitrag – und findet/erstellt die
/// zugehörige Person. Bewusst wie die Konzert-Erfassung (öffentlich sichtbare Person, Rolle Komponist).
/// </summary>
public static class VideoErfassung
{
    /// <summary>Find-or-create Stück (über Titel/Alias); bei neuem Stück optional Komponist:in als Beitrag.</summary>
    public static async Task<Stueck> StueckHolenAsync(ApplicationDbContext db, string titel, string? komponist)
    {
        titel = titel.Trim();
        var vorhanden = await db.Stuecke.FirstOrDefaultAsync(x => x.Titel == titel)
            ?? await db.Stuecke.FirstOrDefaultAsync(x => x.Aliase.Any(a => a.Name == titel));
        if (vorhanden != null) return vorhanden;

        var stueck = new Stueck { Titel = titel };
        db.Stuecke.Add(stueck);
        foreach (var beitrag in KomponistParser.Parse(komponist))
        {
            var person = await PersonHolenAsync(db, beitrag.Name);
            db.StueckBeitraege.Add(new StueckBeitrag { Stueck = stueck, Person = person, Rolle = beitrag.Rolle });
        }
        return stueck;
    }

    /// <summary>Ein Konzert der Band als Zuordnungs-Kandidat für ein Video – inkl. der klein-normalisierten
    /// Programm-Stücktitel (+ Aliase), damit der Abgleich „enthält dieses Stück" in-memory läuft.</summary>
    public record KonzertKandidat(Guid Id, DateOnly Datum, string? Name, string? Ort, HashSet<string> StueckTitel)
    {
        /// <summary>Anzeige „13.05.2024 · Jahreskonzert (Willisau)".</summary>
        public string Anzeige =>
            Datum.ToString("dd.MM.yyyy")
            + (string.IsNullOrWhiteSpace(Name) ? "" : $" · {Name}")
            + (string.IsNullOrWhiteSpace(Ort) ? "" : $" ({Ort})");

        public bool Enthaelt(string? stueckTitel) =>
            !string.IsNullOrWhiteSpace(stueckTitel) && StueckTitel.Contains(stueckTitel.Trim().ToLowerInvariant());
    }

    /// <summary>Lädt die Konzerte, an denen die Band mitwirkt (neueste zuerst), je mit den Programm-Stücktiteln
    /// (+ Aliase) für den In-Memory-Abgleich. Grundlage der automatischen Konzert-Zuordnung eines Videos.</summary>
    public static async Task<List<KonzertKandidat>> BandKonzerteAsync(ApplicationDbContext db, Guid bandId)
    {
        var roh = await db.Konzerte
            .Where(k => k.Bands.Any(b => b.BandId == bandId))
            .OrderByDescending(k => k.Datum)
            .Select(k => new
            {
                k.Id,
                k.Datum,
                k.Name,
                Ort = k.Lokal != null ? k.Lokal.Name : k.Ort,
                Titel = k.Programm.Select(p => p.Stueck.Titel).ToList(),
                Aliase = k.Programm.SelectMany(p => p.Stueck.Aliase.Select(a => a.Name)).ToList()
            })
            .ToListAsync();

        return roh.Select(k => new KonzertKandidat(k.Id, k.Datum, k.Name, k.Ort,
            k.Titel.Concat(k.Aliase).Select(t => t.ToLowerInvariant()).ToHashSet())).ToList();
    }

    /// <summary>Findet – wenn eindeutig – das vergangene/heutige Konzert der Band, dessen Programm das Stück
    /// enthält. Genau ein Treffer → Vorschlag; sonst <c>null</c> (Nutzer:in wählt). <paramref name="heute"/>
    /// begrenzt auf nicht-künftige Konzerte (ein Video kann nicht von einem künftigen Konzert sein).</summary>
    public static Guid? EindeutigesKonzert(IEnumerable<KonzertKandidat> konzerte, string? stueckTitel, DateOnly heute)
    {
        var treffer = konzerte.Where(k => k.Datum <= heute && k.Enthaelt(stueckTitel)).Take(2).ToList();
        return treffer.Count == 1 ? treffer[0].Id : null;
    }

    /// <summary>Find-or-create Komponist:in-Person (öffentlich sichtbar, wie bei der Konzert-Erfassung).</summary>
    public static async Task<Person> PersonHolenAsync(ApplicationDbContext db, string name)
    {
        name = name.Trim();
        var p = await db.Personen.Include(x => x.Rollen).FirstOrDefaultAsync(x => x.Name == name)
            ?? await db.Personen.Include(x => x.Rollen).FirstOrDefaultAsync(x => x.Aliase.Any(a => a.Name == name));
        if (p == null)
        {
            p = new Person { Name = name, Sichtbarkeit = Sichtbarkeit.Oeffentlich };
            db.Personen.Add(p);
        }
        if (p.Rollen.All(r => r.Rolle != PersonRolleTyp.Komponist))
            p.Rollen.Add(new PersonRolle { Rolle = PersonRolleTyp.Komponist });
        return p;
    }
}
